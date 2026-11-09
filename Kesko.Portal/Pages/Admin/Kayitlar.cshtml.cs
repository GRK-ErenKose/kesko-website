using Kesko.Portal.Data;
using Kesko.Portal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Pages.Admin;

public class KayitlarModel : PageModel
{
    private readonly AppDbContext _db;
    public KayitlarModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true, Name = "t")] public KayitTur? Tur { get; set; }
    [BindProperty(SupportsGet = true, Name = "sayfa")] public int Sayfa { get; set; } = 1;

    public SayfaliSonuc<Kayit> Sonuc { get; private set; } = null!;
    public Dictionary<KayitTur, int> TurSayilari { get; private set; } = [];
    public int TumuSayisi { get; private set; }

    public async Task OnGetAsync()
    {
        const int boyut = 50;

        var q = _db.Kayitlar.AsNoTracking().AsQueryable();
        if (Tur is not null) q = q.Where(k => k.Tur == Tur);

        var toplam = await q.CountAsync();
        var sayfaSayisi = Math.Max(1, (int)Math.Ceiling(toplam / (double)boyut));
        var sayfa = Math.Clamp(Sayfa < 1 ? 1 : Sayfa, 1, sayfaSayisi);

        Sonuc = new SayfaliSonuc<Kayit>
        {
            Satirlar = await q.OrderByDescending(k => k.Id)
                              .Skip((sayfa - 1) * boyut).Take(boyut).ToListAsync(),
            Toplam = toplam,
            Sayfa = sayfa,
            Boyut = boyut
        };

        TurSayilari = await _db.Kayitlar.AsNoTracking()
            .GroupBy(k => k.Tur)
            .Select(g => new { g.Key, Sayi = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Sayi);
        TumuSayisi = TurSayilari.Values.Sum();

        ViewData["Baslik"] = "İşlem kayıtları";
        ViewData["Yol"] = "Sistem";
    }

    public string Baglanti(KayitTur? tur, int? sayfa = null)
    {
        var p = new List<string>();
        if (tur is not null) p.Add($"t={tur}");
        if (sayfa is > 1) p.Add($"sayfa={sayfa}");
        return "/admin/kayitlar" + (p.Count == 0 ? "" : "?" + string.Join('&', p));
    }
}
