using Kesko.Portal.Data;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Pages.Admin;

/// <summary>
/// Tek talebin tum ayrintisi. Siparis ve teklif ayni sayfadan gosterilir;
/// yol parcasi ("siparis" / "teklif") hem baglanti tutarliligi hem de
/// yanlis turdeki kaydin acilmamasi icin dogrulanir.
/// </summary>
public class DetayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly MailService _mail;
    private readonly KayitService _kayit;

    public DetayModel(AppDbContext db, MailService mail, KayitService kayit)
    {
        _db = db;
        _mail = mail;
        _kayit = kayit;
    }

    public Talep T { get; private set; } = null!;
    public string Kok { get; private set; } = "siparis";
    public bool SmtpKurulu { get; private set; }
    /// <summary>Ayni firmadan gelen diger talepler.</summary>
    public List<Talep> Gecmis { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string kok, int id)
    {
        var yuklendi = await YukleAsync(kok, id);
        if (yuklendi is not null) return yuklendi;

        ViewData["Baslik"] = T.Baslik;
        ViewData["Yol"] = (T.Tur == TalepTur.Siparis ? "Sipariş talepleri" : "Teklif talepleri") + " · " + T.Referans;
        return Page();
    }

    public async Task<IActionResult> OnPostDurumAsync(string kok, int id, TalepDurum durum)
    {
        var yuklendi = await YukleAsync(kok, id);
        if (yuklendi is not null) return yuklendi;

        if (T.Durum == durum)
        {
            TempData["Bilgi"] = "Talep zaten bu durumda.";
            return Redirect($"/admin/{Kok}/{id}");
        }

        var eski = T.DurumEtiket;
        T.Durum = durum;
        T.GuncellemeUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _kayit.YazAsync(KayitTur.DurumDegisti,
            $"{T.Referans}: '{eski}' → '{T.DurumEtiket}'");

        TempData["Ok"] = $"Durum “{T.DurumEtiket}” olarak güncellendi.";
        return Redirect($"/admin/{Kok}/{id}");
    }

    public async Task<IActionResult> OnPostNotAsync(string kok, int id, string? metin)
    {
        var yuklendi = await YukleAsync(kok, id);
        if (yuklendi is not null) return yuklendi;

        if (string.IsNullOrWhiteSpace(metin))
        {
            TempData["Hata"] = "Not boş olamaz.";
            return Redirect($"/admin/{Kok}/{id}");
        }

        _db.TalepNotlari.Add(new TalepNot
        {
            TalepId = T.Id,
            Metin = metin.Trim().Length > 4000 ? metin.Trim()[..4000] : metin.Trim(),
            Yazan = User.Identity?.Name ?? "—"
        });
        T.GuncellemeUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _kayit.YazAsync(KayitTur.NotEklendi, $"{T.Referans} talebine not eklendi.");
        TempData["Ok"] = "Not eklendi.";
        return Redirect($"/admin/{Kok}/{id}");
    }

    public async Task<IActionResult> OnPostYanitAsync(string kok, int id, string? konu, string? govde)
    {
        var yuklendi = await YukleAsync(kok, id);
        if (yuklendi is not null) return yuklendi;

        if (string.IsNullOrWhiteSpace(konu) || string.IsNullOrWhiteSpace(govde))
        {
            TempData["Hata"] = "Konu ve mesaj alanlarını doldurun.";
            return Redirect($"/admin/{Kok}/{id}");
        }

        var sonuc = await _mail.YanitlaAsync(T, konu.Trim(), govde.Trim());
        if (sonuc.Basarili)
        {
            _db.TalepNotlari.Add(new TalepNot
            {
                TalepId = T.Id,
                Metin = $"E-posta gönderildi — “{konu.Trim()}”\n\n{govde.Trim()}",
                Yazan = User.Identity?.Name ?? "—"
            });
            if (T.Durum == TalepDurum.Yeni) T.Durum = TalepDurum.Incelemede;
            T.GuncellemeUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Ok"] = $"E-posta {T.Eposta} adresine gönderildi.";
        }
        else
        {
            TempData["Hata"] = "E-posta gönderilemedi: " + sonuc.Mesaj;
        }

        return Redirect($"/admin/{Kok}/{id}");
    }

    public async Task<IActionResult> OnPostSilAsync(string kok, int id)
    {
        var yuklendi = await YukleAsync(kok, id);
        if (yuklendi is not null) return yuklendi;

        var referans = T.Referans;
        _db.Talepler.Remove(T);
        await _db.SaveChangesAsync();

        await _kayit.YazAsync(KayitTur.TalepSilindi, $"{referans} kalıcı olarak silindi.");
        TempData["Ok"] = $"{referans} silindi.";
        return Redirect(Kok == "siparis" ? "/admin/siparisler" : "/admin/teklifler");
    }

    /// <summary>
    /// Kaydi yukler. Bulunamazsa ya da tur yol parcasiyla uyusmuyorsa
    /// dondurulecek sonucu verir; uyuyorsa null doner.
    /// </summary>
    private async Task<IActionResult?> YukleAsync(string kok, int id)
    {
        Kok = kok == "teklif" ? "teklif" : "siparis";
        var beklenen = Kok == "teklif" ? TalepTur.Teklif : TalepTur.Siparis;

        var t = await _db.Talepler
            .Include(x => x.Notlar.OrderByDescending(n => n.OlusturmaUtc))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t is null) return NotFound();

        // Dogru sayfaya yonlendir: /admin/siparis/5 ile acilan bir teklif
        // kaydi /admin/teklif/5 adresine tasinir.
        if (t.Tur != beklenen)
            return Redirect($"/admin/{(t.Tur == TalepTur.Siparis ? "siparis" : "teklif")}/{id}");

        T = t;

        var ayar = await _db.SmtpAyarlari.AsNoTracking().FirstAsync();
        SmtpKurulu = !string.IsNullOrWhiteSpace(ayar.Sunucu);

        if (!string.IsNullOrWhiteSpace(t.Firma))
        {
            Gecmis = await _db.Talepler.AsNoTracking()
                .Where(x => x.Firma == t.Firma && x.Id != t.Id)
                .OrderByDescending(x => x.OlusturmaUtc)
                .Take(6)
                .ToListAsync();
        }

        return null;
    }
}
