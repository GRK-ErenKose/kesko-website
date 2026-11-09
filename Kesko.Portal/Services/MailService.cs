using Kesko.Portal.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MimeKit.Text;

namespace Kesko.Portal.Services;

public record MailSonuc(bool Basarili, string Mesaj);

/// <summary>
/// SMTP ayarlari veritabanindan okunur, panelden duzenlenir. Sifre
/// DataProtection ile sifrelenmis saklanir; bu servis disinda cozulmez.
/// </summary>
public class MailService
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _koruyucu;
    private readonly ILogger<MailService> _log;

    public MailService(AppDbContext db, IDataProtectionProvider dp, ILogger<MailService> log)
    {
        _db = db;
        _koruyucu = dp.CreateProtector("Kesko.Portal.Smtp.v1");
        _log = log;
    }

    public string Koru(string duzMetin) => _koruyucu.Protect(duzMetin);

    private string? Coz(string? korumali)
    {
        if (string.IsNullOrEmpty(korumali)) return null;
        try { return _koruyucu.Unprotect(korumali); }
        catch (Exception ex)
        {
            _log.LogError(ex, "SMTP sifresi cozulemedi. DataProtection anahtari degismis olabilir.");
            return null;
        }
    }

    public Task<SmtpAyar> AyarAsync() =>
        _db.SmtpAyarlari.OrderBy(x => x.Id).FirstAsync();

    /// <summary>Yeni talep bildirimi. Hata firlatmaz; sonucu dondurur.</summary>
    public async Task<MailSonuc> TalepBildirAsync(Talep t)
    {
        var ayar = await AyarAsync();
        if (!ayar.BildirimAktif) return new(false, "Bildirim kapalı.");
        if (string.IsNullOrWhiteSpace(ayar.Sunucu)) return new(false, "SMTP sunucusu tanımlı değil.");

        var alicilar = Adresler(ayar.AliciAdresler);
        if (alicilar.Count == 0) return new(false, "Alıcı adresi tanımlı değil.");

        var tur = t.Tur == TalepTur.Siparis ? "Sipariş talebi" : "Toplu tedarik / teklif talebi";
        var konu = $"[Kesko] {tur} — {t.Baslik} ({t.Referans})";

        var ileti = new MimeMessage();
        ileti.From.Add(new MailboxAddress(ayar.GonderenAd, ayar.GonderenAdres));
        foreach (var a in alicilar) ileti.To.Add(MailboxAddress.Parse(a));
        if (!string.IsNullOrWhiteSpace(t.Eposta))
            ileti.ReplyTo.Add(MailboxAddress.Parse(t.Eposta));
        else if (!string.IsNullOrWhiteSpace(ayar.YanitAdresi))
            ileti.ReplyTo.Add(MailboxAddress.Parse(ayar.YanitAdresi));
        ileti.Subject = konu;
        ileti.Body = new TextPart(TextFormat.Html) { Text = BildirimGovdesi(t, tur) };

        var sonuc = await GonderAsync(ayar, ileti);

        if (sonuc.Basarili && ayar.OtomatikYanitAktif && !string.IsNullOrWhiteSpace(t.Eposta))
            _ = OtomatikYanitAsync(ayar, t);

        return sonuc;
    }

    private async Task OtomatikYanitAsync(SmtpAyar ayar, Talep t)
    {
        try
        {
            var govde = (ayar.OtomatikYanitGovde ?? "")
                .Replace("{Yetkili}", t.Yetkili ?? t.Firma ?? "")
                .Replace("{Firma}", t.Firma ?? "")
                .Replace("{Referans}", t.Referans);

            var ileti = new MimeMessage();
            ileti.From.Add(new MailboxAddress(ayar.GonderenAd, ayar.GonderenAdres));
            ileti.To.Add(MailboxAddress.Parse(t.Eposta!));
            if (!string.IsNullOrWhiteSpace(ayar.YanitAdresi))
                ileti.ReplyTo.Add(MailboxAddress.Parse(ayar.YanitAdresi));
            ileti.Subject = ayar.OtomatikYanitKonu ?? "Talebiniz bize ulaştı";
            ileti.Body = new TextPart(TextFormat.Plain) { Text = govde };

            await GonderAsync(ayar, ileti);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Otomatik yanit gonderilemedi: {Ref}", t.Referans);
        }
    }

    /// <summary>Panelden talebe serbest metinle yanit verir.</summary>
    public async Task<MailSonuc> YanitlaAsync(Talep t, string konu, string govde)
    {
        var ayar = await AyarAsync();
        if (string.IsNullOrWhiteSpace(ayar.Sunucu)) return new(false, "SMTP sunucusu tanımlı değil.");
        if (string.IsNullOrWhiteSpace(t.Eposta)) return new(false, "Talepte e-posta adresi yok.");

        var ileti = new MimeMessage();
        ileti.From.Add(new MailboxAddress(ayar.GonderenAd, ayar.GonderenAdres));
        ileti.To.Add(MailboxAddress.Parse(t.Eposta));
        foreach (var a in Adresler(ayar.AliciAdresler)) ileti.Bcc.Add(MailboxAddress.Parse(a));
        if (!string.IsNullOrWhiteSpace(ayar.YanitAdresi))
            ileti.ReplyTo.Add(MailboxAddress.Parse(ayar.YanitAdresi));
        ileti.Subject = konu;
        ileti.Body = new TextPart(TextFormat.Plain) { Text = govde };

        return await GonderAsync(ayar, ileti);
    }

    /// <summary>
    /// Ayarlar sayfasindaki "test gonder" dugmesi. Sifre alani bos
    /// birakildiysa kayitli sifre kullanilir.
    /// </summary>
    public async Task<MailSonuc> TestGonderAsync(SmtpAyar taslak, string? duzSifre, string hedef)
    {
        if (string.IsNullOrWhiteSpace(taslak.Sunucu)) return new(false, "Sunucu adresi girin.");
        if (string.IsNullOrWhiteSpace(taslak.GonderenAdres)) return new(false, "Gönderen adresi girin.");
        if (string.IsNullOrWhiteSpace(hedef)) return new(false, "Test için hedef adres girin.");

        var ileti = new MimeMessage();
        ileti.From.Add(new MailboxAddress(taslak.GonderenAd, taslak.GonderenAdres));
        ileti.To.Add(MailboxAddress.Parse(hedef));
        ileti.Subject = "[Kesko] SMTP test e-postası";
        ileti.Body = new TextPart(TextFormat.Html)
        {
            Text = Sablon("SMTP ayarları çalışıyor",
                $"""
                <p>Bu e-posta Kesko yönetim panelindeki <strong>SMTP testi</strong> ile gönderildi.</p>
                <table>
                  <tr><td>Sunucu</td><td>{Kacis(taslak.Sunucu)}:{taslak.Port}</td></tr>
                  <tr><td>Güvenlik</td><td>{Kacis(taslak.Guvenlik)}</td></tr>
                  <tr><td>Gönderen</td><td>{Kacis(taslak.GonderenAdres)}</td></tr>
                  <tr><td>Zaman</td><td>{DateTime.Now:dd.MM.yyyy HH:mm:ss}</td></tr>
                </table>
                """)
        };

        var kullan = taslak;
        if (string.IsNullOrEmpty(duzSifre))
        {
            // Panelde sifre alani bos: kayitli sifreyle dene.
            var kayitli = await AyarAsync();
            kullan = Kopya(taslak, kayitli.SifreKorumali);
        }
        else
        {
            kullan = Kopya(taslak, Koru(duzSifre));
        }

        return await GonderAsync(kullan, ileti);
    }

    private static SmtpAyar Kopya(SmtpAyar k, string? sifreKorumali) => new()
    {
        Sunucu = k.Sunucu,
        Port = k.Port,
        Guvenlik = k.Guvenlik,
        KullaniciAdi = k.KullaniciAdi,
        SifreKorumali = sifreKorumali,
        GonderenAdres = k.GonderenAdres,
        GonderenAd = k.GonderenAd,
        AliciAdresler = k.AliciAdresler,
        YanitAdresi = k.YanitAdresi
    };

    private async Task<MailSonuc> GonderAsync(SmtpAyar ayar, MimeMessage ileti)
    {
        try
        {
            var secenek = ayar.Guvenlik switch
            {
                "ssl" => SecureSocketOptions.SslOnConnect,
                "none" => SecureSocketOptions.None,
                _ => SecureSocketOptions.StartTls
            };

            using var istemci = new SmtpClient { Timeout = 20_000 };
            await istemci.ConnectAsync(ayar.Sunucu, ayar.Port, secenek);

            if (!string.IsNullOrWhiteSpace(ayar.KullaniciAdi))
            {
                var sifre = Coz(ayar.SifreKorumali);
                if (sifre is null)
                    return new(false, "Kayıtlı SMTP şifresi okunamadı. Şifreyi yeniden girin.");
                await istemci.AuthenticateAsync(ayar.KullaniciAdi, sifre);
            }

            await istemci.SendAsync(ileti);
            await istemci.DisconnectAsync(true);
            return new(true, "Gönderildi.");
        }
        catch (AuthenticationException ex)
        {
            _log.LogWarning(ex, "SMTP kimlik dogrulama hatasi");
            return new(false, "Kimlik doğrulama başarısız: kullanıcı adı veya şifre hatalı.");
        }
        catch (SmtpCommandException ex)
        {
            _log.LogWarning(ex, "SMTP komut hatasi");
            return new(false, $"SMTP hatası ({ex.StatusCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Mail gonderilemedi");
            return new(false, Kisalt(ex.Message, 300));
        }
    }

    private static List<string> Adresler(string? ham) =>
        (ham ?? "")
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(a => a.Contains('@'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string Kisalt(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    private static string Kacis(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string BildirimGovdesi(Talep t, string tur)
    {
        var satirlar = new List<(string, string?)>
        {
            ("Referans", t.Referans),
            ("Firma", t.Firma),
            ("Yetkili", t.Yetkili),
            ("E-posta", t.Eposta),
            ("Telefon", t.Telefon),
            ("Şehir", t.Sehir)
        };

        if (t.Tur == TalepTur.Siparis)
        {
            satirlar.Add(("Ürün", t.Urun));
            satirlar.Add(("Gramaj", t.Gramaj));
            satirlar.Add(("Adet", t.Adet?.ToString("N0")));
            satirlar.Add(("Bölge", t.BolgeEtiket));
            satirlar.Add(("Kargo", t.KargoBedava ? "Bedava" : "Ücretli"));
        }
        else
        {
            satirlar.Add(("İlgilenilen hat", t.Hat));
            satirlar.Add(("Gramaj", t.Gramaj));
        }

        satirlar.Add(("Geliş zamanı", t.OlusturmaUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")));

        var tablo = string.Join("", satirlar
            .Where(s => !string.IsNullOrWhiteSpace(s.Item2))
            .Select(s => $"<tr><td>{Kacis(s.Item1)}</td><td>{Kacis(s.Item2)}</td></tr>"));

        var mesaj = string.IsNullOrWhiteSpace(t.Mesaj)
            ? ""
            : $"<h3>Mesaj</h3><p class=\"msg\">{Kacis(t.Mesaj).Replace("\n", "<br>")}</p>";

        return Sablon(tur, $"<table>{tablo}</table>{mesaj}");
    }

    /// <summary>Marka renklerine uygun, inline stilli e-posta sablonu.</summary>
    private static string Sablon(string baslik, string icerik) =>
        $$"""
        <!doctype html><html lang="tr"><head><meta charset="utf-8">
        <style>
          body{margin:0;background:#F4F4F7;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#1A1A33}
          .k{max-width:620px;margin:0 auto;background:#fff}
          .hd{background:#2B2971;color:#F8E300;padding:26px 30px;font-size:13px;letter-spacing:.18em;text-transform:uppercase;font-weight:700}
          .bd{padding:28px 30px}
          h2{margin:0 0 18px;font-size:23px;letter-spacing:-.02em;color:#2B2971}
          h3{margin:24px 0 8px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#67678A}
          table{width:100%;border-collapse:collapse;font-size:14.5px}
          td{padding:10px 0;border-bottom:1px solid #E4E4EA;vertical-align:top}
          td:first-child{color:#67678A;width:40%}
          td:last-child{font-weight:600}
          .msg{font-size:14.5px;line-height:1.65;background:#F4F4F7;padding:14px 16px;border-left:3px solid #F8E300;margin:0}
          .ft{padding:18px 30px;background:#1C1A4D;color:#9A9ACB;font-size:12px}
        </style></head><body>
        <div class="k">
          <div class="hd">Kesko Gıda</div>
          <div class="bd"><h2>{{Kacis(baslik)}}</h2>{{icerik}}</div>
          <div class="ft">Bu e-posta kesko.com.tr üzerindeki form tarafından otomatik oluşturuldu.</div>
        </div></body></html>
        """;
}
