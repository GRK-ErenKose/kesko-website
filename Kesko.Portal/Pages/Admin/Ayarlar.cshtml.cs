using Kesko.Portal.Data;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Pages.Admin;

public class AyarlarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly MailService _mail;
    private readonly KayitService _kayit;

    public AyarlarModel(AppDbContext db, MailService mail, KayitService kayit)
    {
        _db = db;
        _mail = mail;
        _kayit = kayit;
    }

    [BindProperty] public SmtpAyar Smtp { get; set; } = new();
    [BindProperty] public SiteAyar Site { get; set; } = new();
    /// <summary>Formdan gelen duz metin sifre. Bos ise kayitli sifre korunur.</summary>
    [BindProperty] public string? YeniSifre { get; set; }
    [BindProperty] public string? TestAdres { get; set; }

    public bool SifreKayitli { get; private set; }

    public async Task OnGetAsync()
    {
        await YukleAsync();
        Baslik();
    }

    /// <summary>SMTP ve bildirim ayarlarini kaydeder.</summary>
    public async Task<IActionResult> OnPostSmtpAsync()
    {
        var kayitli = await _db.SmtpAyarlari.OrderBy(x => x.Id).FirstAsync();

        kayitli.Sunucu = (Smtp.Sunucu ?? "").Trim();
        kayitli.Port = Smtp.Port is > 0 and < 65536 ? Smtp.Port : 587;
        kayitli.Guvenlik = Smtp.Guvenlik is "none" or "ssl" or "starttls" ? Smtp.Guvenlik : "starttls";
        kayitli.KullaniciAdi = (Smtp.KullaniciAdi ?? "").Trim();
        kayitli.GonderenAdres = (Smtp.GonderenAdres ?? "").Trim();
        kayitli.GonderenAd = string.IsNullOrWhiteSpace(Smtp.GonderenAd) ? "Kesko Web" : Smtp.GonderenAd.Trim();
        kayitli.AliciAdresler = (Smtp.AliciAdresler ?? "").Trim();
        kayitli.YanitAdresi = string.IsNullOrWhiteSpace(Smtp.YanitAdresi) ? null : Smtp.YanitAdresi.Trim();
        kayitli.BildirimAktif = Smtp.BildirimAktif;
        kayitli.OtomatikYanitAktif = Smtp.OtomatikYanitAktif;
        kayitli.OtomatikYanitKonu = Smtp.OtomatikYanitKonu;
        kayitli.OtomatikYanitGovde = Smtp.OtomatikYanitGovde;

        // Sifre alani bos bırakildiysa mevcut sifre korunur.
        if (!string.IsNullOrEmpty(YeniSifre))
            kayitli.SifreKorumali = _mail.Koru(YeniSifre);

        if (kayitli.BildirimAktif && string.IsNullOrWhiteSpace(kayitli.Sunucu))
        {
            kayitli.BildirimAktif = false;
            TempData["Hata"] = "Sunucu adresi girilmediği için bildirim açılamadı.";
        }
        else
        {
            TempData["Ok"] = "SMTP ayarları kaydedildi.";
        }

        await _db.SaveChangesAsync();
        await _kayit.YazAsync(KayitTur.AyarDegisti,
            $"SMTP ayarları güncellendi. Sunucu: {(kayitli.Sunucu.Length == 0 ? "—" : kayitli.Sunucu)}:{kayitli.Port}, " +
            $"bildirim: {(kayitli.BildirimAktif ? "açık" : "kapalı")}");

        return RedirectToPage();
    }

    /// <summary>Formdaki ayarlarla tek bir test e-postasi gonderir.</summary>
    public async Task<IActionResult> OnPostTestAsync()
    {
        var hedef = (TestAdres ?? "").Trim();
        if (hedef.Length == 0)
        {
            var mevcut = await _db.SmtpAyarlari.AsNoTracking().FirstAsync();
            hedef = mevcut.AliciAdresler.Split([',', ';'], StringSplitOptions.TrimEntries)
                .FirstOrDefault(a => a.Contains('@')) ?? "";
        }

        var sonuc = await _mail.TestGonderAsync(Smtp, YeniSifre, hedef);

        var kayitli = await _db.SmtpAyarlari.OrderBy(x => x.Id).FirstAsync();
        kayitli.SonTestUtc = DateTime.UtcNow;
        kayitli.SonTestBasarili = sonuc.Basarili;
        kayitli.SonTestSonuc = sonuc.Basarili ? $"Başarılı — {hedef}" : sonuc.Mesaj;
        await _db.SaveChangesAsync();

        await _kayit.YazAsync(KayitTur.TestMaili,
            $"Test e-postası {(sonuc.Basarili ? "gönderildi" : "gönderilemedi")}: {hedef} — {sonuc.Mesaj}");

        if (sonuc.Basarili) TempData["Ok"] = $"Test e-postası {hedef} adresine gönderildi.";
        else TempData["Hata"] = sonuc.Mesaj;

        return RedirectToPage();
    }

    /// <summary>Statik sitenin panelden yonetilen sabitlerini kaydeder.</summary>
    public async Task<IActionResult> OnPostSiteAsync()
    {
        var kayitli = await _db.SiteAyarlari.OrderBy(x => x.Id).FirstAsync();

        kayitli.KargoBedavaEsik = Math.Clamp(Site.KargoBedavaEsik, 1, 1_000_000);
        kayitli.WhatsappNumara = new string((Site.WhatsappNumara ?? "").Where(char.IsDigit).ToArray());
        kayitli.IletisimEposta = (Site.IletisimEposta ?? "").Trim();
        kayitli.IletisimTelefon = (Site.IletisimTelefon ?? "").Trim();
        kayitli.FormlarAktif = Site.FormlarAktif;
        kayitli.BakimMesaji = string.IsNullOrWhiteSpace(Site.BakimMesaji) ? null : Site.BakimMesaji.Trim();

        await _db.SaveChangesAsync();
        await _kayit.YazAsync(KayitTur.AyarDegisti,
            $"Site ayarları güncellendi. Kargo bedava eşiği: {kayitli.KargoBedavaEsik}, " +
            $"formlar: {(kayitli.FormlarAktif ? "açık" : "kapalı")}");

        TempData["Ok"] = "Site ayarları kaydedildi.";
        return RedirectToPage();
    }

    private async Task YukleAsync()
    {
        Smtp = await _db.SmtpAyarlari.AsNoTracking().OrderBy(x => x.Id).FirstAsync();
        Site = await _db.SiteAyarlari.AsNoTracking().OrderBy(x => x.Id).FirstAsync();
        SifreKayitli = !string.IsNullOrEmpty(Smtp.SifreKorumali);
    }

    private void Baslik()
    {
        ViewData["Baslik"] = "SMTP & site ayarları";
        ViewData["Yol"] = "Sistem";
    }
}
