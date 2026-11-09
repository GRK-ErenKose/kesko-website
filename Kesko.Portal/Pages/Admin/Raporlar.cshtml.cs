using Kesko.Portal.Data;
using Kesko.Portal.Models;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kesko.Portal.Pages.Admin;

public class RaporlarModel : PageModel
{
    private readonly RaporService _rapor;
    private readonly TalepService _talepler;
    private readonly KayitService _kayit;

    public RaporlarModel(RaporService rapor, TalepService talepler, KayitService kayit)
    {
        _rapor = rapor;
        _talepler = talepler;
        _kayit = kayit;
    }

    [BindProperty(SupportsGet = true, Name = "bas")] public DateOnly? Bas { get; set; }
    [BindProperty(SupportsGet = true, Name = "bit")] public DateOnly? Bit { get; set; }
    [BindProperty(SupportsGet = true, Name = "tur")] public TalepTur? Tur { get; set; }
    /// <summary>Hazir aralik: 7 | 30 | 90 | ay | geceneay | yil</summary>
    [BindProperty(SupportsGet = true, Name = "a")] public string? Aralik { get; set; }

    public RaporVerisi V { get; private set; } = null!;
    public DateOnly Baslangic { get; private set; }
    public DateOnly Bitis { get; private set; }

    public async Task OnGetAsync()
    {
        (Baslangic, Bitis) = AralikCoz();
        V = await _rapor.RaporAsync(Baslangic, Bitis, Tur);

        ViewData["Baslik"] = "Raporlar";
        ViewData["Yol"] = "Genel";
    }

    /// <summary>Secili aralikla filtrelenmis tum kayitlari CSV olarak indirir.</summary>
    public async Task<IActionResult> OnGetCsvAsync()
    {
        (Baslangic, Bitis) = AralikCoz();

        var filtre = new TalepFiltre { Baslangic = Baslangic, Bitis = Bitis, Boyut = 250 };
        var veri = await _talepler.CsvAsync(Tur, filtre);

        await _kayit.YazAsync(KayitTur.Disaaktarim,
            $"Rapor CSV olarak indirildi: {Baslangic:dd.MM.yyyy}–{Bitis:dd.MM.yyyy}, tür={Tur?.ToString() ?? "tümü"}");

        return File(veri, "text/csv; charset=utf-8",
            $"kesko-rapor-{Baslangic:yyyyMMdd}-{Bitis:yyyyMMdd}.csv");
    }

    /// <summary>Hazir aralik secildiyse tarihleri ondan uretir.</summary>
    private (DateOnly, DateOnly) AralikCoz()
    {
        var bugun = DateOnly.FromDateTime(DateTime.Now);

        if (Bas is not null && Bit is not null && string.IsNullOrEmpty(Aralik))
            return (Bas.Value, Bit.Value);

        return Aralik switch
        {
            "7" => (bugun.AddDays(-6), bugun),
            "90" => (bugun.AddDays(-89), bugun),
            "ay" => (new DateOnly(bugun.Year, bugun.Month, 1), bugun),
            "geceneay" => (
                new DateOnly(bugun.Year, bugun.Month, 1).AddMonths(-1),
                new DateOnly(bugun.Year, bugun.Month, 1).AddDays(-1)),
            "yil" => (new DateOnly(bugun.Year, 1, 1), bugun),
            _ => (Bas ?? bugun.AddDays(-29), Bit ?? bugun)
        };
    }

    /// <summary>Aralik dugmeleri icin baglanti.</summary>
    public string AralikBag(string a) =>
        $"/admin/raporlar?a={a}" + (Tur is null ? "" : $"&tur={Tur}");

    public bool AralikEtkin(string a)
    {
        if (!string.IsNullOrEmpty(Aralik)) return Aralik == a;
        // Varsayilan gorunum 30 gun.
        return a == "30" && Bas is null && Bit is null;
    }

    public string TurBag(TalepTur? t)
    {
        var p = new List<string>();
        if (!string.IsNullOrEmpty(Aralik)) p.Add($"a={Aralik}");
        else
        {
            p.Add($"bas={Baslangic:yyyy-MM-dd}");
            p.Add($"bit={Bitis:yyyy-MM-dd}");
        }
        if (t is not null) p.Add($"tur={t}");
        return "/admin/raporlar?" + string.Join('&', p);
    }

    public string CsvBag()
    {
        var p = new List<string> { "handler=Csv" };
        if (!string.IsNullOrEmpty(Aralik)) p.Add($"a={Aralik}");
        else
        {
            p.Add($"bas={Baslangic:yyyy-MM-dd}");
            p.Add($"bit={Bitis:yyyy-MM-dd}");
        }
        if (Tur is not null) p.Add($"tur={Tur}");
        return "/admin/raporlar?" + string.Join('&', p);
    }
}
