using System.ComponentModel.DataAnnotations;
using Kesko.Portal.Data;
using Kesko.Portal.Services;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Api;

// --- gelen govdeler ---------------------------------------------------------

public class SiparisIstek
{
    [MaxLength(60)] public string? Urun { get; set; }
    [MaxLength(40)] public string? Gramaj { get; set; }
    public int Adet { get; set; }
    [MaxLength(10)] public string? Bolge { get; set; }
    [MaxLength(120)] public string? Sehir { get; set; }
    [MaxLength(120)] public string? Yetkili { get; set; }
    [MaxLength(160)] public string? Firma { get; set; }
    [MaxLength(160)] public string? Eposta { get; set; }
    [MaxLength(40)] public string? Telefon { get; set; }
    public string? Mesaj { get; set; }
    /// <summary>Bal kupu: insan doldurmaz, bot doldurur.</summary>
    public string? Website { get; set; }
}

public class TeklifIstek
{
    [MaxLength(160)] public string? Firma { get; set; }
    [MaxLength(120)] public string? Yetkili { get; set; }
    [MaxLength(160)] public string? Eposta { get; set; }
    [MaxLength(40)] public string? Telefon { get; set; }
    [MaxLength(120)] public string? Sehir { get; set; }
    [MaxLength(80)] public string? Hat { get; set; }
    [MaxLength(40)] public string? Gramaj { get; set; }
    public string? Mesaj { get; set; }
    public string? Website { get; set; }
}

public record FormYanit(bool ok, string mesaj, string? referans = null);

/// <summary>
/// Statik sitedeki iki formun sunucu tarafi. Istemci dogrulamasi guvenlik
/// degildir: her alan burada tekrar dogrulanir, bal kupu ve IP basina
/// hiz siniri uygulanir.
/// </summary>
public static class FormEndpoints
{
    // IP basina son gonderim zamanlari. Tek ornek icin bellek yeterli.
    private static readonly Dictionary<string, List<DateTime>> _gecmis = new();
    private static readonly Lock _kilit = new();
    private const int PencereDakika = 10;
    private const int PencereLimit = 5;

    public static void MapFormEndpoints(this IEndpointRouteBuilder app)
    {
        var grup = app.MapGroup("/api").WithTags("Form");

        grup.MapGet("/site-ayar", async (AppDbContext db) =>
        {
            var a = await db.SiteAyarlari.AsNoTracking().FirstAsync();
            return Results.Ok(new
            {
                kargoBedavaEsik = a.KargoBedavaEsik,
                whatsapp = a.WhatsappNumara,
                eposta = a.IletisimEposta,
                telefon = a.IletisimTelefon,
                formlarAktif = a.FormlarAktif,
                bakimMesaji = a.BakimMesaji
            });
        });

        grup.MapPost("/siparis", async (
            SiparisIstek istek, HttpContext http, AppDbContext db, MailService mail, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("Form.Siparis");
            var ip = KayitService.Ip(http);

            if (!string.IsNullOrWhiteSpace(istek.Website))
            {
                log.LogInformation("Bal kupu doldu, istek sessizce yutuldu. IP {Ip}", ip);
                return Results.Ok(new FormYanit(true, "Talebiniz alındı."));
            }

            if (!HizUygun(ip))
                return Results.Json(new FormYanit(false,
                    "Çok sık gönderim yapıldı. Lütfen birkaç dakika sonra tekrar deneyin."),
                    statusCode: StatusCodes.Status429TooManyRequests);

            var ayar = await db.SiteAyarlari.AsNoTracking().FirstAsync();
            if (!ayar.FormlarAktif)
                return Results.Json(new FormYanit(false,
                    ayar.BakimMesaji ?? "Form geçici olarak kapalı. Lütfen telefonla ulaşın."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var hatalar = new List<string>();
            if (istek.Adet < 1 || istek.Adet > 1_000_000) hatalar.Add("Adet 1 ile 1.000.000 arasında olmalı.");
            if (string.IsNullOrWhiteSpace(istek.Urun)) hatalar.Add("Ürün seçin.");
            if (string.IsNullOrWhiteSpace(istek.Yetkili)) hatalar.Add("Ad soyad girin.");
            if (!EpostaGecerli(istek.Eposta) && !TelefonGecerli(istek.Telefon))
                hatalar.Add("Size dönebilmemiz için geçerli bir e-posta ya da telefon girin.");
            if (hatalar.Count > 0)
                return Results.BadRequest(new FormYanit(false, string.Join(" ", hatalar)));

            var talep = new Talep
            {
                Tur = TalepTur.Siparis,
                Referans = await db.SonrakiReferansAsync(),
                Urun = Kirp(istek.Urun, 60),
                Gramaj = Kirp(istek.Gramaj, 40),
                Adet = istek.Adet,
                Bolge = istek.Bolge is "ic" or "dis" ? istek.Bolge : null,
                Sehir = Kirp(istek.Sehir, 120),
                Firma = Kirp(istek.Firma, 160),
                Yetkili = Kirp(istek.Yetkili, 120),
                Eposta = EpostaGecerli(istek.Eposta) ? Kirp(istek.Eposta, 160) : null,
                Telefon = Kirp(istek.Telefon, 40),
                Mesaj = Kirp(istek.Mesaj, 4000),
                KargoBedava = istek.Adet >= ayar.KargoBedavaEsik,
                Ip = ip,
                UserAgent = Kirp(http.Request.Headers.UserAgent.ToString(), 400),
                Referrer = Kirp(http.Request.Headers.Referer.ToString(), 400)
            };

            return await KaydetAsync(db, mail, talep, log);
        }).DisableAntiforgery();

        grup.MapPost("/teklif", async (
            TeklifIstek istek, HttpContext http, AppDbContext db, MailService mail, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("Form.Teklif");
            var ip = KayitService.Ip(http);

            if (!string.IsNullOrWhiteSpace(istek.Website))
                return Results.Ok(new FormYanit(true, "Talebiniz alındı."));

            if (!HizUygun(ip))
                return Results.Json(new FormYanit(false,
                    "Çok sık gönderim yapıldı. Lütfen birkaç dakika sonra tekrar deneyin."),
                    statusCode: StatusCodes.Status429TooManyRequests);

            var ayar = await db.SiteAyarlari.AsNoTracking().FirstAsync();
            if (!ayar.FormlarAktif)
                return Results.Json(new FormYanit(false,
                    ayar.BakimMesaji ?? "Form geçici olarak kapalı. Lütfen telefonla ulaşın."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var hatalar = new List<string>();
            if (string.IsNullOrWhiteSpace(istek.Firma)) hatalar.Add("Firma adı girin.");
            if (string.IsNullOrWhiteSpace(istek.Yetkili)) hatalar.Add("Yetkili kişi girin.");
            if (!EpostaGecerli(istek.Eposta)) hatalar.Add("Geçerli bir e-posta girin.");
            if (hatalar.Count > 0)
                return Results.BadRequest(new FormYanit(false, string.Join(" ", hatalar)));

            var talep = new Talep
            {
                Tur = TalepTur.Teklif,
                Referans = await db.SonrakiReferansAsync(),
                Firma = Kirp(istek.Firma, 160),
                Yetkili = Kirp(istek.Yetkili, 120),
                Eposta = Kirp(istek.Eposta, 160),
                Telefon = Kirp(istek.Telefon, 40),
                Sehir = Kirp(istek.Sehir, 120),
                Hat = Kirp(istek.Hat, 80),
                Gramaj = Kirp(istek.Gramaj, 40),
                Mesaj = Kirp(istek.Mesaj, 4000),
                Ip = ip,
                UserAgent = Kirp(http.Request.Headers.UserAgent.ToString(), 400),
                Referrer = Kirp(http.Request.Headers.Referer.ToString(), 400)
            };

            return await KaydetAsync(db, mail, talep, log);
        }).DisableAntiforgery();
    }

    /// <summary>
    /// Kaydi yazar, ardindan bildirimi dener. Bildirim basarisiz olsa da
    /// kullaniciya basari doner: talep veritabaninda duruyor.
    /// </summary>
    private static async Task<IResult> KaydetAsync(
        AppDbContext db, MailService mail, Talep talep, ILogger log)
    {
        db.Talepler.Add(talep);
        await db.SaveChangesAsync();

        try
        {
            var sonuc = await mail.TalepBildirAsync(talep);
            talep.BildirimGonderildi = sonuc.Basarili;
            talep.BildirimHata = sonuc.Basarili ? null : sonuc.Mesaj;
            await db.SaveChangesAsync();
            if (!sonuc.Basarili)
                log.LogWarning("Bildirim gonderilemedi ({Ref}): {Mesaj}", talep.Referans, sonuc.Mesaj);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Bildirim asamasinda beklenmeyen hata ({Ref})", talep.Referans);
        }

        return Results.Ok(new FormYanit(true,
            "Talebiniz bize ulaştı. En kısa sürede dönüş yapacağız.", talep.Referans));
    }

    private static bool HizUygun(string ip)
    {
        var simdi = DateTime.UtcNow;
        lock (_kilit)
        {
            if (!_gecmis.TryGetValue(ip, out var liste))
                _gecmis[ip] = liste = new List<DateTime>();

            liste.RemoveAll(z => (simdi - z).TotalMinutes > PencereDakika);
            if (liste.Count >= PencereLimit) return false;
            liste.Add(simdi);

            // Sozluk sinirsiz buyumesin.
            if (_gecmis.Count > 5000)
                foreach (var k in _gecmis.Where(p => p.Value.Count == 0).Select(p => p.Key).ToList())
                    _gecmis.Remove(k);

            return true;
        }
    }

    private static bool EpostaGecerli(string? e) =>
        !string.IsNullOrWhiteSpace(e) && new EmailAddressAttribute().IsValid(e) && e.Length <= 160;

    private static bool TelefonGecerli(string? t) =>
        !string.IsNullOrWhiteSpace(t) && t.Count(char.IsDigit) >= 10;

    private static string? Kirp(string? s, int n)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= n ? s : s[..n];
    }
}
