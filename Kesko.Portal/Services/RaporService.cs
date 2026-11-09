using Kesko.Portal.Data;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Services;

public record ZamanNoktasi(DateOnly Gun, int Siparis, int Teklif, int Adet)
{
    public int Toplam => Siparis + Teklif;
}

public record Dilim(string Etiket, int Deger, int? Adet = null);

public record Kpi(string Etiket, string Deger, double? Degisim, string? AltMetin, int[] Seri);

public record GostergeVerisi(
    List<Kpi> Kpiler,
    List<ZamanNoktasi> Seri,
    List<Dilim> UrunDagilimi,
    List<Dilim> HatDagilimi,
    List<Dilim> SehirDagilimi,
    List<Dilim> DurumDagilimi,
    List<Talep> SonTalepler,
    int BekleyenSayisi,
    int SmtpHataSayisi,
    bool SmtpKurulu);

public record RaporVerisi(
    DateOnly Baslangic,
    DateOnly Bitis,
    int ToplamTalep,
    int SiparisSayisi,
    int TeklifSayisi,
    int ToplamAdet,
    double OrtalamaAdet,
    int MedyanAdet,
    double DonusumOrani,
    List<ZamanNoktasi> Seri,
    List<Dilim> UrunDagilimi,
    List<Dilim> GramajDagilimi,
    List<Dilim> BolgeDagilimi,
    List<Dilim> HatDagilimi,
    List<Dilim> SehirDagilimi,
    List<Dilim> DurumDagilimi,
    List<Dilim> SaatDagilimi,
    List<Dilim> HaftaGunuDagilimi,
    List<(string Firma, int Talep, int Adet, string? Sehir, DateTime Son)> EnAktifFirmalar,
    int OncekiDonemTalep,
    int OncekiDonemAdet);

/// <summary>
/// Gosterge paneli ve rapor sayfasinin tum toplamlarini uretir.
/// Gruplamalar yerel saate gore yapildigi icin satirlar bellege alinip
/// burada toplaniyor; form hacmi bunun icin fazlasiyla uygun.
/// </summary>
public class RaporService
{
    private readonly AppDbContext _db;
    public RaporService(AppDbContext db) => _db = db;

    public async Task<GostergeVerisi> GostergeAsync()
    {
        var bugun = DateOnly.FromDateTime(DateTime.Now);
        var pencere = bugun.AddDays(-29);
        var pencereUtc = pencere.ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var son30 = await _db.Talepler.AsNoTracking()
            .Where(t => t.OlusturmaUtc >= pencereUtc && t.Durum != TalepDurum.Spam)
            .ToListAsync();

        var seri = Seri(son30, pencere, bugun);

        int Say(Func<Talep, bool> p) => son30.Count(p);
        bool Gun(Talep t, DateOnly g) => DateOnly.FromDateTime(t.OlusturmaUtc.ToLocalTime()) == g;
        bool Arasi(Talep t, DateOnly a, DateOnly b)
        {
            var g = DateOnly.FromDateTime(t.OlusturmaUtc.ToLocalTime());
            return g >= a && g <= b;
        }

        var bugunSayi = Say(t => Gun(t, bugun));
        var dunSayi = Say(t => Gun(t, bugun.AddDays(-1)));
        var son7 = Say(t => Arasi(t, bugun.AddDays(-6), bugun));
        var onceki7 = Say(t => Arasi(t, bugun.AddDays(-13), bugun.AddDays(-7)));
        var son7Adet = son30.Where(t => Arasi(t, bugun.AddDays(-6), bugun)).Sum(t => t.Adet ?? 0);
        var onceki7Adet = son30.Where(t => Arasi(t, bugun.AddDays(-13), bugun.AddDays(-7))).Sum(t => t.Adet ?? 0);

        var bekleyen = await _db.Talepler.CountAsync(t => t.Durum == TalepDurum.Yeni);
        var kazanilan = await _db.Talepler.CountAsync(t => t.Durum == TalepDurum.Kazanildi);
        var kapanan = await _db.Talepler.CountAsync(t =>
            t.Durum == TalepDurum.Kazanildi || t.Durum == TalepDurum.Kaybedildi);
        var donusum = kapanan == 0 ? 0 : Math.Round(kazanilan * 100.0 / kapanan, 1);

        var son14Seri = seri.TakeLast(14).Select(s => s.Toplam).ToArray();
        var son14Adet = seri.TakeLast(14).Select(s => s.Adet).ToArray();

        var kpiler = new List<Kpi>
        {
            new("Bugün gelen", bugunSayi.ToString("N0"), Fark(bugunSayi, dunSayi),
                $"Dün {dunSayi:N0}", son14Seri),
            new("Son 7 gün", son7.ToString("N0"), Fark(son7, onceki7),
                $"Önceki hafta {onceki7:N0}", son14Seri),
            new("Son 7 gün adet", son7Adet.ToString("N0"), Fark(son7Adet, onceki7Adet),
                $"Önceki hafta {onceki7Adet:N0} adet", son14Adet),
            new("Bekleyen talep", bekleyen.ToString("N0"), null,
                bekleyen == 0 ? "Kutu temiz" : "Durumu güncellenmemiş", son14Seri),
            new("Dönüşüm", donusum.ToString("0.#") + "%", null,
                $"{kazanilan:N0} kazanıldı / {kapanan:N0} kapandı", son14Seri)
        };

        var tumu = _db.Talepler.AsNoTracking().Where(t => t.Durum != TalepDurum.Spam);

        var urun = await Grupla(tumu.Where(t => t.Urun != null), t => t.Urun!);
        var hat = await Grupla(tumu.Where(t => t.Hat != null), t => t.Hat!);
        var sehir = await Grupla(tumu.Where(t => t.Sehir != null && t.Sehir != ""), t => t.Sehir!, 8);

        var durum = await _db.Talepler.AsNoTracking()
            .GroupBy(t => t.Durum)
            .Select(g => new { g.Key, Sayi = g.Count() })
            .ToListAsync();
        var durumDagilim = durum
            .OrderBy(d => d.Key)
            .Select(d => new Dilim(new Talep { Durum = d.Key }.DurumEtiket, d.Sayi))
            .ToList();

        var sonTalepler = await _db.Talepler.AsNoTracking()
            .OrderByDescending(t => t.OlusturmaUtc)
            .Take(8)
            .ToListAsync();

        var smtp = await _db.SmtpAyarlari.AsNoTracking().FirstAsync();
        var smtpHata = await _db.Talepler.CountAsync(t => t.BildirimHata != null);

        return new GostergeVerisi(kpiler, seri, urun, hat, sehir, durumDagilim, sonTalepler,
            bekleyen, smtpHata, !string.IsNullOrWhiteSpace(smtp.Sunucu) && smtp.BildirimAktif);
    }

    public async Task<RaporVerisi> RaporAsync(DateOnly bas, DateOnly bit, TalepTur? tur)
    {
        if (bit < bas) (bas, bit) = (bit, bas);

        var basUtc = bas.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var bitUtc = bit.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var gunSayisi = bit.DayNumber - bas.DayNumber + 1;
        var oncekiBas = bas.AddDays(-gunSayisi);
        var oncekiBasUtc = oncekiBas.ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var hepsi = await _db.Talepler.AsNoTracking()
            .Where(t => t.OlusturmaUtc >= oncekiBasUtc && t.OlusturmaUtc < bitUtc)
            .Where(t => tur == null || t.Tur == tur)
            .Where(t => t.Durum != TalepDurum.Spam)
            .ToListAsync();

        var donem = hepsi.Where(t => t.OlusturmaUtc >= basUtc).ToList();
        var onceki = hepsi.Where(t => t.OlusturmaUtc < basUtc).ToList();

        var adetler = donem.Where(t => t.Adet > 0).Select(t => t.Adet!.Value).OrderBy(x => x).ToList();
        var medyan = adetler.Count == 0 ? 0
            : adetler.Count % 2 == 1 ? adetler[adetler.Count / 2]
            : (adetler[adetler.Count / 2 - 1] + adetler[adetler.Count / 2]) / 2;

        var kazanilan = donem.Count(t => t.Durum == TalepDurum.Kazanildi);
        var kapanan = donem.Count(t => t.Durum is TalepDurum.Kazanildi or TalepDurum.Kaybedildi);

        List<Dilim> Grup(Func<Talep, string?> secici, int? ilk = null)
        {
            var q = donem
                .Where(t => !string.IsNullOrWhiteSpace(secici(t)))
                .GroupBy(secici)
                .Select(g => new Dilim(g.Key!, g.Count(), g.Sum(t => t.Adet ?? 0)))
                .OrderByDescending(d => d.Deger)
                .ToList();
            return ilk is null ? q : q.Take(ilk.Value).ToList();
        }

        var saatler = Enumerable.Range(0, 24)
            .Select(s => new Dilim($"{s:00}", donem.Count(t => t.OlusturmaUtc.ToLocalTime().Hour == s)))
            .ToList();

        string[] gunAdi = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
        var haftaGunu = Enumerable.Range(0, 7)
            .Select(i => new Dilim(gunAdi[i], donem.Count(t =>
                ((int)t.OlusturmaUtc.ToLocalTime().DayOfWeek + 6) % 7 == i)))
            .ToList();

        var firmalar = donem
            .Where(t => !string.IsNullOrWhiteSpace(t.Firma))
            .GroupBy(t => t.Firma!)
            .Select(g => (
                Firma: g.Key,
                Talep: g.Count(),
                Adet: g.Sum(t => t.Adet ?? 0),
                Sehir: g.Select(t => t.Sehir).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                Son: g.Max(t => t.OlusturmaUtc)))
            .OrderByDescending(x => x.Talep).ThenByDescending(x => x.Adet)
            .Take(10)
            .ToList();

        var durumDagilim = donem
            .GroupBy(t => t.Durum)
            .OrderBy(g => g.Key)
            .Select(g => new Dilim(new Talep { Durum = g.Key }.DurumEtiket, g.Count()))
            .ToList();

        var bolge = donem
            .Where(t => !string.IsNullOrWhiteSpace(t.Bolge))
            .GroupBy(t => t.BolgeEtiket)
            .Select(g => new Dilim(g.Key, g.Count(), g.Sum(t => t.Adet ?? 0)))
            .OrderByDescending(d => d.Deger)
            .ToList();

        return new RaporVerisi(
            bas, bit,
            donem.Count,
            donem.Count(t => t.Tur == TalepTur.Siparis),
            donem.Count(t => t.Tur == TalepTur.Teklif),
            donem.Sum(t => t.Adet ?? 0),
            adetler.Count == 0 ? 0 : Math.Round(adetler.Average(), 1),
            medyan,
            kapanan == 0 ? 0 : Math.Round(kazanilan * 100.0 / kapanan, 1),
            Seri(donem, bas, bit),
            Grup(t => t.Urun),
            Grup(t => t.Gramaj),
            bolge,
            Grup(t => t.Hat),
            Grup(t => t.Sehir, 12),
            durumDagilim,
            saatler,
            haftaGunu,
            firmalar,
            onceki.Count,
            onceki.Sum(t => t.Adet ?? 0));
    }

    /// <summary>Gun bosluklarini sifirla dolduran zaman serisi.</summary>
    private static List<ZamanNoktasi> Seri(List<Talep> kayitlar, DateOnly bas, DateOnly bit)
    {
        var sozluk = kayitlar
            .GroupBy(t => DateOnly.FromDateTime(t.OlusturmaUtc.ToLocalTime()))
            .ToDictionary(g => g.Key, g => (
                Siparis: g.Count(t => t.Tur == TalepTur.Siparis),
                Teklif: g.Count(t => t.Tur == TalepTur.Teklif),
                Adet: g.Sum(t => t.Adet ?? 0)));

        var liste = new List<ZamanNoktasi>();
        for (var g = bas; g <= bit; g = g.AddDays(1))
        {
            if (sozluk.TryGetValue(g, out var v)) liste.Add(new ZamanNoktasi(g, v.Siparis, v.Teklif, v.Adet));
            else liste.Add(new ZamanNoktasi(g, 0, 0, 0));
        }
        return liste;
    }

    private static async Task<List<Dilim>> Grupla(
        IQueryable<Talep> q, System.Linq.Expressions.Expression<Func<Talep, string>> secici, int? ilk = null)
    {
        var sonuc = await q.GroupBy(secici)
            .Select(g => new { Etiket = g.Key, Sayi = g.Count(), Adet = g.Sum(t => t.Adet ?? 0) })
            .OrderByDescending(x => x.Sayi)
            .Take(ilk ?? 20)
            .ToListAsync();
        return sonuc.Select(x => new Dilim(x.Etiket, x.Sayi, x.Adet)).ToList();
    }

    private static double? Fark(int simdi, int once)
    {
        if (once == 0) return simdi == 0 ? 0 : null;   // null = "yeni", oran anlamsiz
        return Math.Round((simdi - once) * 100.0 / once, 1);
    }
}
