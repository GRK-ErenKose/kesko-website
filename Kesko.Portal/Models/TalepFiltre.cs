using Kesko.Portal.Data;
using Microsoft.AspNetCore.Mvc;

namespace Kesko.Portal.Models;

/// <summary>
/// Liste sayfalarinin ortak filtre sozlesmesi. Querystring'den baglanir,
/// sayfalar arasinda tasinir ve CSV aktariminda aynen yeniden kullanilir.
/// </summary>
public class TalepFiltre
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Arama { get; set; }
    [BindProperty(SupportsGet = true, Name = "d")] public TalepDurum? Durum { get; set; }
    [BindProperty(SupportsGet = true, Name = "bas")] public DateOnly? Baslangic { get; set; }
    [BindProperty(SupportsGet = true, Name = "bit")] public DateOnly? Bitis { get; set; }
    [BindProperty(SupportsGet = true, Name = "u")] public string? Urun { get; set; }
    [BindProperty(SupportsGet = true, Name = "g")] public string? Gramaj { get; set; }
    [BindProperty(SupportsGet = true, Name = "b")] public string? Bolge { get; set; }
    [BindProperty(SupportsGet = true, Name = "h")] public string? Hat { get; set; }
    [BindProperty(SupportsGet = true, Name = "s")] public string? Sehir { get; set; }
    [BindProperty(SupportsGet = true, Name = "min")] public int? AdetMin { get; set; }
    [BindProperty(SupportsGet = true, Name = "max")] public int? AdetMax { get; set; }
    /// <summary>tarih | adet | firma | durum</summary>
    [BindProperty(SupportsGet = true, Name = "sira")] public string Sira { get; set; } = "tarih";
    [BindProperty(SupportsGet = true, Name = "yon")] public string Yon { get; set; } = "desc";
    [BindProperty(SupportsGet = true, Name = "sayfa")] public int Sayfa { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "boyut")] public int Boyut { get; set; } = 25;

    public bool FiltreVarMi =>
        !string.IsNullOrWhiteSpace(Arama) || Durum is not null || Baslangic is not null ||
        Bitis is not null || !string.IsNullOrWhiteSpace(Urun) || !string.IsNullOrWhiteSpace(Gramaj) ||
        !string.IsNullOrWhiteSpace(Bolge) || !string.IsNullOrWhiteSpace(Hat) ||
        !string.IsNullOrWhiteSpace(Sehir) || AdetMin is not null || AdetMax is not null;

    /// <summary>Sayfalama ve siralama baglantilari icin querystring sozlugu.</summary>
    public Dictionary<string, string?> Route(params (string Anahtar, string? Deger)[] degisiklik)
    {
        var d = new Dictionary<string, string?>();
        void Ek(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) d[k] = v; }

        Ek("q", Arama);
        Ek("d", Durum?.ToString());
        Ek("bas", Baslangic?.ToString("yyyy-MM-dd"));
        Ek("bit", Bitis?.ToString("yyyy-MM-dd"));
        Ek("u", Urun);
        Ek("g", Gramaj);
        Ek("b", Bolge);
        Ek("h", Hat);
        Ek("s", Sehir);
        Ek("min", AdetMin?.ToString());
        Ek("max", AdetMax?.ToString());
        Ek("sira", Sira);
        Ek("yon", Yon);
        if (Boyut != 25) Ek("boyut", Boyut.ToString());
        if (Sayfa > 1) Ek("sayfa", Sayfa.ToString());

        foreach (var (k, v) in degisiklik)
        {
            if (string.IsNullOrWhiteSpace(v)) d.Remove(k);
            else d[k] = v;
        }
        return d;
    }
}

public class SayfaliSonuc<T>
{
    public required List<T> Satirlar { get; init; }
    public int Toplam { get; init; }
    public int Sayfa { get; init; }
    public int Boyut { get; init; }
    public int SayfaSayisi => Math.Max(1, (int)Math.Ceiling(Toplam / (double)Boyut));
    public int Ilk => Toplam == 0 ? 0 : (Sayfa - 1) * Boyut + 1;
    public int Son => Math.Min(Sayfa * Boyut, Toplam);
}
