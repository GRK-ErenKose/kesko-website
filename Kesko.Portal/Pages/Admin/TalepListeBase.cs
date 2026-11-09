using Kesko.Portal.Data;
using Kesko.Portal.Models;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Pages.Admin;

/// <summary>
/// Siparis ve teklif listelerinin ortak davranisi: filtreleme, sayfalama,
/// toplu durum degistirme ve CSV aktarimi. Iki sayfa da yalnizca kendi
/// turunu ve metinlerini belirtir.
/// </summary>
public abstract class TalepListeBase : PageModel
{
    protected readonly AppDbContext Db;
    protected readonly TalepService Talepler;
    protected readonly KayitService Kayit;

    protected TalepListeBase(AppDbContext db, TalepService talepler, KayitService kayit)
    {
        Db = db;
        Talepler = talepler;
        Kayit = kayit;
    }

    /// <summary>Sayfanin listeledigi tur.</summary>
    public abstract TalepTur Tur { get; }
    /// <summary>Detay baglantisinin koku: "siparis" ya da "teklif".</summary>
    public abstract string Kok { get; }
    /// <summary>Liste sayfasinin yolu: "/admin/siparisler" ya da "/admin/teklifler".</summary>
    public abstract string ListeYolu { get; }

    /// <summary>
    /// Mevcut filtreyi koruyarak, verilen anahtarlari degistiren baglanti uretir.
    /// Siralama basliklari, sayfalama ve sekmeler bunu kullanir.
    /// </summary>
    public string Baglanti(params (string Anahtar, string? Deger)[] degisiklik) =>
        Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(ListeYolu, F.Route(degisiklik));

    [BindProperty(SupportsGet = true)] public TalepFiltre F { get; set; } = new();

    public SayfaliSonuc<Talep> Sonuc { get; private set; } = null!;
    public List<string> Urunler { get; private set; } = [];
    public List<string> Gramajlar { get; private set; } = [];
    public List<string> Hatlar { get; private set; } = [];
    public List<string> Sehirler { get; private set; } = [];
    public Dictionary<TalepDurum, int> DurumSayilari { get; private set; } = [];
    public int TumuSayisi { get; private set; }
    public int ToplamAdet { get; private set; }

    protected async Task YukleAsync()
    {
        Sonuc = await Talepler.ListeleAsync(Tur, F);
        (Urunler, Gramajlar, Hatlar, Sehirler) = await Talepler.SecenekAsync(Tur);

        // Sekme sayaclari: durum filtresi dışındaki filtreler korunur.
        var sayacFiltre = new TalepFiltre
        {
            Arama = F.Arama, Baslangic = F.Baslangic, Bitis = F.Bitis,
            Urun = F.Urun, Gramaj = F.Gramaj, Bolge = F.Bolge, Hat = F.Hat,
            Sehir = F.Sehir, AdetMin = F.AdetMin, AdetMax = F.AdetMax
        };
        var taban = Talepler.Sorgu(Tur, sayacFiltre);

        DurumSayilari = await taban
            .GroupBy(t => t.Durum)
            .Select(g => new { g.Key, Sayi = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Sayi);
        TumuSayisi = DurumSayilari.Values.Sum();

        ToplamAdet = await Talepler.Sorgu(Tur, F).SumAsync(t => t.Adet ?? 0);
    }

    public async Task OnGetAsync() => await YukleAsync();

    /// <summary>Filtreye uyan tum kayitlari CSV olarak indirir.</summary>
    public async Task<IActionResult> OnGetCsvAsync()
    {
        var veri = await Talepler.CsvAsync(Tur, F);
        await Kayit.YazAsync(KayitTur.Disaaktarim,
            $"{Kok} listesi CSV olarak indirildi. Filtre: {Ozet()}");

        var dosya = $"kesko-{Kok}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
        return File(veri, "text/csv; charset=utf-8", dosya);
    }

    /// <summary>Secili taleplerin durumunu topluca degistirir.</summary>
    public async Task<IActionResult> OnPostTopluAsync(int[] secili, TalepDurum? yeniDurum)
    {
        if (secili.Length == 0 || yeniDurum is null)
        {
            TempData["Hata"] = "Değiştirilecek talep ya da yeni durum seçilmedi.";
            return Redirect(Baglanti());
        }

        var kayitlar = await Db.Talepler.Where(t => secili.Contains(t.Id) && t.Tur == Tur).ToListAsync();
        foreach (var t in kayitlar)
        {
            t.Durum = yeniDurum.Value;
            t.GuncellemeUtc = DateTime.UtcNow;
        }
        await Db.SaveChangesAsync();

        await Kayit.YazAsync(KayitTur.DurumDegisti,
            $"{kayitlar.Count} talep '{new Talep { Durum = yeniDurum.Value }.DurumEtiket}' olarak işaretlendi.");

        TempData["Ok"] = $"{kayitlar.Count} talep güncellendi.";
        return Redirect(Baglanti());
    }

    private string Ozet()
    {
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(F.Arama)) p.Add($"arama='{F.Arama}'");
        if (F.Durum is not null) p.Add($"durum={F.Durum}");
        if (F.Baslangic is not null) p.Add($"başlangıç={F.Baslangic:dd.MM.yyyy}");
        if (F.Bitis is not null) p.Add($"bitiş={F.Bitis:dd.MM.yyyy}");
        if (!string.IsNullOrWhiteSpace(F.Urun)) p.Add($"ürün={F.Urun}");
        if (!string.IsNullOrWhiteSpace(F.Hat)) p.Add($"hat={F.Hat}");
        if (!string.IsNullOrWhiteSpace(F.Sehir)) p.Add($"şehir={F.Sehir}");
        return p.Count == 0 ? "yok" : string.Join(", ", p);
    }
}
