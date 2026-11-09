using System.Globalization;
using System.Text;
using Kesko.Portal.Data;
using Kesko.Portal.Models;
using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Services;

/// <summary>
/// Talep sorgulama, filtreleme, sayfalama ve CSV aktarimi. Filtre mantigi
/// tek yerde: liste sayfasi ile dis aktarim her zaman ayni sonucu verir.
/// </summary>
public class TalepService
{
    private readonly AppDbContext _db;
    public TalepService(AppDbContext db) => _db = db;

    public IQueryable<Talep> Sorgu(TalepTur? tur, TalepFiltre f)
    {
        var q = _db.Talepler.AsNoTracking().AsQueryable();

        if (tur is not null) q = q.Where(t => t.Tur == tur);
        if (f.Durum is not null) q = q.Where(t => t.Durum == f.Durum);

        if (f.Baslangic is not null)
        {
            var bas = f.Baslangic.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            q = q.Where(t => t.OlusturmaUtc >= bas);
        }
        if (f.Bitis is not null)
        {
            var bit = f.Bitis.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            q = q.Where(t => t.OlusturmaUtc < bit);
        }

        if (!string.IsNullOrWhiteSpace(f.Urun)) q = q.Where(t => t.Urun == f.Urun);
        if (!string.IsNullOrWhiteSpace(f.Gramaj)) q = q.Where(t => t.Gramaj == f.Gramaj);
        if (!string.IsNullOrWhiteSpace(f.Bolge)) q = q.Where(t => t.Bolge == f.Bolge);
        if (!string.IsNullOrWhiteSpace(f.Hat)) q = q.Where(t => t.Hat == f.Hat);
        if (!string.IsNullOrWhiteSpace(f.Sehir)) q = q.Where(t => t.Sehir != null && t.Sehir.Contains(f.Sehir));
        if (f.AdetMin is not null) q = q.Where(t => t.Adet >= f.AdetMin);
        if (f.AdetMax is not null) q = q.Where(t => t.Adet <= f.AdetMax);

        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var a = f.Arama.Trim();
            q = q.Where(t =>
                (t.Firma != null && t.Firma.Contains(a)) ||
                (t.Yetkili != null && t.Yetkili.Contains(a)) ||
                (t.Eposta != null && t.Eposta.Contains(a)) ||
                (t.Telefon != null && t.Telefon.Contains(a)) ||
                (t.Sehir != null && t.Sehir.Contains(a)) ||
                (t.Mesaj != null && t.Mesaj.Contains(a)) ||
                t.Referans.Contains(a));
        }

        var artan = f.Yon == "asc";
        q = f.Sira switch
        {
            "adet" => artan ? q.OrderBy(t => t.Adet).ThenBy(t => t.Id)
                            : q.OrderByDescending(t => t.Adet).ThenByDescending(t => t.Id),
            "firma" => artan ? q.OrderBy(t => t.Firma).ThenBy(t => t.Id)
                             : q.OrderByDescending(t => t.Firma).ThenByDescending(t => t.Id),
            "durum" => artan ? q.OrderBy(t => t.Durum).ThenByDescending(t => t.OlusturmaUtc)
                             : q.OrderByDescending(t => t.Durum).ThenByDescending(t => t.OlusturmaUtc),
            _ => artan ? q.OrderBy(t => t.OlusturmaUtc).ThenBy(t => t.Id)
                       : q.OrderByDescending(t => t.OlusturmaUtc).ThenByDescending(t => t.Id)
        };

        return q;
    }

    public async Task<SayfaliSonuc<Talep>> ListeleAsync(TalepTur? tur, TalepFiltre f)
    {
        var boyut = f.Boyut is 10 or 25 or 50 or 100 or 250 ? f.Boyut : 25;
        var q = Sorgu(tur, f);
        var toplam = await q.CountAsync();
        var sayfaSayisi = Math.Max(1, (int)Math.Ceiling(toplam / (double)boyut));
        var sayfa = Math.Clamp(f.Sayfa < 1 ? 1 : f.Sayfa, 1, sayfaSayisi);

        var satirlar = await q.Skip((sayfa - 1) * boyut).Take(boyut).ToListAsync();
        return new SayfaliSonuc<Talep>
        {
            Satirlar = satirlar,
            Toplam = toplam,
            Sayfa = sayfa,
            Boyut = boyut
        };
    }

    /// <summary>Filtre acilir kutularini besleyen, veride gercekten var olan degerler.</summary>
    public async Task<(List<string> Urunler, List<string> Gramajlar, List<string> Hatlar, List<string> Sehirler)>
        SecenekAsync(TalepTur? tur)
    {
        var q = _db.Talepler.AsNoTracking().Where(t => tur == null || t.Tur == tur);
        return (
            await q.Where(t => t.Urun != null).Select(t => t.Urun!).Distinct().OrderBy(x => x).ToListAsync(),
            await q.Where(t => t.Gramaj != null).Select(t => t.Gramaj!).Distinct().OrderBy(x => x).ToListAsync(),
            await q.Where(t => t.Hat != null).Select(t => t.Hat!).Distinct().OrderBy(x => x).ToListAsync(),
            await q.Where(t => t.Sehir != null && t.Sehir != "").Select(t => t.Sehir!).Distinct().OrderBy(x => x).Take(200).ToListAsync()
        );
    }

    /// <summary>
    /// Excel'in bolgesel ayarlarini gozeten CSV. Ayirici noktali virgul,
    /// basta UTF-8 BOM, Turkce karakterler korunur.
    /// </summary>
    public async Task<byte[]> CsvAsync(TalepTur? tur, TalepFiltre f)
    {
        var satirlar = await Sorgu(tur, f).Take(20_000).ToListAsync();
        var sb = new StringBuilder();

        string[] basliklar =
        [
            "Referans", "Tür", "Durum", "Tarih", "Firma", "Yetkili", "E-posta", "Telefon",
            "Şehir", "Ürün", "Gramaj", "Adet", "Bölge", "Kargo", "Hat", "Mesaj", "Bildirim"
        ];
        sb.AppendLine(string.Join(';', basliklar));

        foreach (var t in satirlar)
        {
            string[] h =
            [
                t.Referans,
                t.Tur == TalepTur.Siparis ? "Sipariş" : "Teklif",
                t.DurumEtiket,
                t.OlusturmaUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")),
                t.Firma ?? "", t.Yetkili ?? "", t.Eposta ?? "", t.Telefon ?? "", t.Sehir ?? "",
                t.Urun ?? "", t.Gramaj ?? "", t.Adet?.ToString() ?? "", t.BolgeEtiket,
                t.Tur == TalepTur.Siparis ? (t.KargoBedava ? "Bedava" : "Ücretli") : "",
                t.Hat ?? "", t.Mesaj ?? "",
                t.BildirimGonderildi ? "Gönderildi" : (t.BildirimHata ?? "—")
            ];
            sb.AppendLine(string.Join(';', h.Select(Hucre)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Hucre(string deger)
    {
        var temiz = deger.Replace("\r", " ").Replace("\n", " ").Trim();
        // Hucre basindaki =,+,-,@ Excel'de formul olarak yorumlanir.
        if (temiz.Length > 0 && temiz[0] is '=' or '+' or '-' or '@') temiz = "'" + temiz;
        return temiz.Contains(';') || temiz.Contains('"')
            ? '"' + temiz.Replace("\"", "\"\"") + '"'
            : temiz;
    }
}
