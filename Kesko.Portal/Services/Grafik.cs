using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Html;

namespace Kesko.Portal.Services;

/// <summary>
/// Grafikler el ile SVG olarak uretiliyor: harici kutuphane yok, ciktinin
/// rengi ve tipografisi panelin jetonlariyla ayni, yazdirmada ve koyu
/// temada bozulmuyor. Tum sayilar InvariantCulture ile yazilir; SVG ondalik
/// ayirici olarak nokta bekler.
/// </summary>
public static class Grafik
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Grafik dizisindeki renkler; CSS'teki --c1..--c8 ile ayni.</summary>
    public static readonly string[] Renkler =
        ["#2B2971", "#F8E300", "#1FA39A", "#E9603F", "#7B5BD6", "#3D8BD4", "#8DAE2B", "#D64C8C"];

    private static string S(double d) => d.ToString("0.##", Inv);
    private static string Kacis(string s) => System.Net.WebUtility.HtmlEncode(s);

    // ---------------------------------------------------------------- kivilcim
    /// <summary>KPI kartinin altindaki minik egri. Eksen ve etiket yok.</summary>
    public static IHtmlContent Kivilcim(int[] seri, string renk = "#2B2971")
    {
        if (seri.Length < 2) return HtmlString.Empty;

        const double g = 180, y = 42, alt = 3;
        var enb = seri.Max();
        var enk = seri.Min();
        var aralik = Math.Max(1, enb - enk);
        var adim = g / (seri.Length - 1);

        var nokta = seri.Select((v, i) =>
            $"{S(i * adim)},{S(alt + (y - alt * 2) * (1 - (v - enk) / (double)aralik))}");
        var cizgi = string.Join(" ", nokta);
        var kimlik = "kv" + Guid.NewGuid().ToString("N")[..6];

        return new HtmlString($"""
            <svg class="kiv" viewBox="0 0 {S(g)} {S(y)}" preserveAspectRatio="none" aria-hidden="true">
              <defs><linearGradient id="{kimlik}" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" stop-color="{renk}" stop-opacity=".22"/>
                <stop offset="1" stop-color="{renk}" stop-opacity="0"/>
              </linearGradient></defs>
              <polygon points="0,{S(y)} {cizgi} {S(g)},{S(y)}" fill="url(#{kimlik})"/>
              <polyline points="{cizgi}" fill="none" stroke="{renk}" stroke-width="1.8"
                        stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>
            </svg>
            """);
    }

    // ------------------------------------------------------------- alan grafik
    /// <summary>
    /// Gunluk talep akisi. Ikinci seri (adet) istege bagli, kesikli cizgi
    /// olarak ikinci bir olcekle cizilir.
    /// </summary>
    public static IHtmlContent Alan(List<ZamanNoktasi> seri, bool adetSerisi = false)
    {
        if (seri.Count == 0) return Bos("Bu aralıkta veri yok");

        const double G = 820, Y = 260;
        const double sol = 42, sag = 14, ust = 16, dip = 30;
        var ig = G - sol - sag;
        var iy = Y - ust - dip;

        var degerler = seri.Select(s => s.Toplam).ToList();
        var enb = Math.Max(1, degerler.Max());
        var tavan = Tavan(enb);
        var adim = seri.Count > 1 ? ig / (seri.Count - 1) : 0;

        double X(int i) => sol + i * adim;
        double Yk(double v) => ust + iy * (1 - v / tavan);

        // Gradyan kimligi her cizimde benzersiz: ayni sayfada iki alan
        // grafigi bulunursa dolgular birbirine karismaz.
        var dolgu = "dolgu" + Guid.NewGuid().ToString("N")[..6];

        var sb = new StringBuilder();
        sb.Append($"""
            <svg class="grafik" viewBox="0 0 {S(G)} {S(Y)}" role="img"
                 aria-label="Günlük talep akışı grafiği">
              <defs>
                <linearGradient id="{dolgu}" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0" stop-color="#2B2971" stop-opacity=".26"/>
                  <stop offset="1" stop-color="#2B2971" stop-opacity="0"/>
                </linearGradient>
              </defs>
            """);

        // yatay izgara + y eksen etiketleri
        for (var k = 0; k <= 4; k++)
        {
            var v = tavan * k / 4;
            var yk = Yk(v);
            sb.Append($"""<line class="izgara-cizgi" x1="{S(sol)}" y1="{S(yk)}" x2="{S(G - sag)}" y2="{S(yk)}"/>""");
            sb.Append($"""<text class="eksen" x="{S(sol - 8)}" y="{S(yk + 3.5)}" text-anchor="end">{S(v)}</text>""");
        }

        // alan + cizgi
        var noktalar = string.Join(" ", seri.Select((s, i) => $"{S(X(i))},{S(Yk(s.Toplam))}"));
        sb.Append($"""<polygon fill="url(#{dolgu})" points="{S(sol)},{S(ust + iy)} {noktalar} {S(X(seri.Count - 1))},{S(ust + iy)}"/>""");
        sb.Append($"""<polyline class="cizgi-1" points="{noktalar}"/>""");

        if (adetSerisi)
        {
            var adetTavan = Math.Max(1, seri.Max(s => s.Adet));
            var adetNokta = string.Join(" ", seri.Select((s, i) =>
                $"{S(X(i))},{S(ust + iy * (1 - s.Adet / (double)adetTavan))}"));
            sb.Append($"""<polyline class="cizgi-2" points="{adetNokta}"/>""");
        }

        // x eksen etiketleri: en fazla 8 tane, esit araliklarla
        var atla = Math.Max(1, (int)Math.Ceiling(seri.Count / 8.0));
        for (var i = 0; i < seri.Count; i += atla)
        {
            var g = seri[i].Gun;
            sb.Append($"""<text class="eksen" x="{S(X(i))}" y="{S(Y - 10)}" text-anchor="middle">{g.Day}.{g.Month}</text>""");
        }

        // fare hedefleri: dikdortgen genis, nokta kucuk
        for (var i = 0; i < seri.Count; i++)
        {
            var s = seri[i];
            var ipucu =
                $"{s.Gun.ToString("dd MMMM yyyy", Tr)}\n" +
                $"Toplam: {s.Toplam}  (sipariş {s.Siparis}, teklif {s.Teklif})" +
                (adetSerisi ? $"\nAdet: {s.Adet.ToString("N0", Tr)}" : "");
            sb.Append($"""
                <g data-ipucu="{Kacis(ipucu)}">
                  <rect x="{S(X(i) - adim / 2)}" y="{S(ust)}" width="{S(Math.Max(adim, 6))}" height="{S(iy)}" fill="transparent"/>
                  <circle class="nokta" cx="{S(X(i))}" cy="{S(Yk(s.Toplam))}" r="{(seri.Count > 45 ? 0 : 3.2)}"/>
                </g>
                """);
        }

        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    // ------------------------------------------------------------------- halka
    /// <summary>Oran gostermek icin halka grafik. Dilimler stroke ile cizilir.</summary>
    public static IHtmlContent Halka(List<Dilim> dilimler, string? ortaUst = null, string? ortaAlt = null)
    {
        var toplam = dilimler.Sum(d => d.Deger);
        if (toplam == 0) return Bos("Veri yok");

        const double mrk = 84, r = 60, kal = 19;
        var cevre = 2 * Math.PI * r;
        var sb = new StringBuilder();

        sb.Append($"""
            <svg viewBox="0 0 {S(mrk * 2)} {S(mrk * 2)}" role="img" aria-label="Oran grafiği">
              <circle cx="{S(mrk)}" cy="{S(mrk)}" r="{S(r)}" fill="none"
                      stroke="var(--hair-2)" stroke-width="{S(kal)}"/>
              <g transform="rotate(-90 {S(mrk)} {S(mrk)})">
            """);

        double birikim = 0;
        for (var i = 0; i < dilimler.Count; i++)
        {
            var d = dilimler[i];
            if (d.Deger == 0) continue;
            var oran = d.Deger / (double)toplam;
            var uzunluk = cevre * oran;
            var renk = Renkler[i % Renkler.Length];
            var ipucu = $"{d.Etiket}\n{d.Deger.ToString("N0", Tr)} talep  ·  %{(oran * 100).ToString("0.#", Tr)}";

            sb.Append($"""
                <circle data-ipucu="{Kacis(ipucu)}" cx="{S(mrk)}" cy="{S(mrk)}" r="{S(r)}" fill="none"
                        stroke="{renk}" stroke-width="{S(kal)}"
                        stroke-dasharray="{S(uzunluk)} {S(cevre - uzunluk)}"
                        stroke-dashoffset="{S(-birikim)}" style="cursor:default"/>
                """);
            birikim += uzunluk;
        }

        sb.Append("</g>");

        if (ortaUst is not null)
            sb.Append($"""
                <text x="{S(mrk)}" y="{S(mrk - 2)}" text-anchor="middle"
                      style="font-family:var(--d);font-weight:800;font-size:25px;fill:var(--ink)">{Kacis(ortaUst)}</text>
                """);
        if (ortaAlt is not null)
            sb.Append($"""
                <text x="{S(mrk)}" y="{S(mrk + 17)}" text-anchor="middle"
                      style="font-family:var(--b);font-weight:600;font-size:10.5px;letter-spacing:.1em;fill:var(--muted)">{Kacis(ortaAlt.ToUpperInvariant())}</text>
                """);

        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Halkanin yanindaki renk aciklamasi.</summary>
    public static IHtmlContent Aciklama(List<Dilim> dilimler)
    {
        var toplam = Math.Max(1, dilimler.Sum(d => d.Deger));
        var sb = new StringBuilder("<ul class=\"aciklama\">");
        for (var i = 0; i < dilimler.Count; i++)
        {
            var d = dilimler[i];
            sb.Append($"""
                <li>
                  <i style="background:{Renkler[i % Renkler.Length]}"></i>
                  <span class="ad" title="{Kacis(d.Etiket)}">{Kacis(d.Etiket)}</span>
                  <span class="sayi">{d.Deger.ToString("N0", Tr)}</span>
                  <span class="yuzde">%{(d.Deger * 100.0 / toplam).ToString("0.#", Tr)}</span>
                </li>
                """);
        }
        sb.Append("</ul>");
        return new HtmlString(sb.ToString());
    }

    // ------------------------------------------------------------------ cubuk
    /// <summary>Dikey cubuk grafik: saat ve hafta gunu dagilimlari icin.</summary>
    public static IHtmlContent Cubuk(List<Dilim> dilimler, string birim = "talep")
    {
        if (dilimler.Count == 0 || dilimler.All(d => d.Deger == 0))
            return Bos("Veri yok");

        const double G = 820, Y = 190;
        const double sol = 34, sag = 10, ust = 12, dip = 26;
        var ig = G - sol - sag;
        var iy = Y - ust - dip;

        var tavan = Tavan(Math.Max(1, dilimler.Max(d => d.Deger)));
        var genislik = ig / dilimler.Count;
        var cubuk = Math.Min(genislik * 0.62, 34);

        var sb = new StringBuilder($"""
            <svg class="grafik" viewBox="0 0 {S(G)} {S(Y)}" role="img" aria-label="Dağılım grafiği">
            """);

        for (var k = 0; k <= 3; k++)
        {
            var v = tavan * k / 3;
            var yk = ust + iy * (1 - v / tavan);
            sb.Append($"""<line class="izgara-cizgi" x1="{S(sol)}" y1="{S(yk)}" x2="{S(G - sag)}" y2="{S(yk)}"/>""");
            sb.Append($"""<text class="eksen" x="{S(sol - 7)}" y="{S(yk + 3.5)}" text-anchor="end">{S(v)}</text>""");
        }

        for (var i = 0; i < dilimler.Count; i++)
        {
            var d = dilimler[i];
            var x = sol + i * genislik + (genislik - cubuk) / 2;
            var h = iy * d.Deger / tavan;
            var ipucu = $"{d.Etiket}\n{d.Deger.ToString("N0", Tr)} {birim}";

            sb.Append($"""
                <g data-ipucu="{Kacis(ipucu)}">
                  <rect x="{S(sol + i * genislik)}" y="{S(ust)}" width="{S(genislik)}" height="{S(iy)}" fill="transparent"/>
                  <rect class="cubuk" x="{S(x)}" y="{S(ust + iy - h)}" width="{S(cubuk)}"
                        height="{S(Math.Max(h, d.Deger > 0 ? 2 : 0))}" rx="3"/>
                </g>
                """);

            var atla = dilimler.Count > 16 ? 2 : 1;
            if (i % atla == 0)
                sb.Append($"""<text class="eksen" x="{S(sol + i * genislik + genislik / 2)}" y="{S(Y - 9)}" text-anchor="middle">{Kacis(d.Etiket)}</text>""");
        }

        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    // ----------------------------------------------------------- yatay cubuk
    /// <summary>Siralanmis liste: urun, sehir, hat dagilimlari icin.</summary>
    public static IHtmlContent YatayCubuk(List<Dilim> dilimler, bool adetGoster = false)
    {
        if (dilimler.Count == 0) return Bos("Veri yok");

        var enb = Math.Max(1, dilimler.Max(d => d.Deger));
        var sb = new StringBuilder("<ul class=\"cubuklar\">");
        foreach (var d in dilimler)
        {
            var oran = d.Deger * 100.0 / enb;
            var adet = adetGoster && d.Adet is > 0
                ? $"<small>{d.Adet!.Value.ToString("N0", Tr)} adet</small>"
                : "";
            sb.Append($"""
                <li>
                  <span class="ad" title="{Kacis(d.Etiket)}">{Kacis(d.Etiket)}</span>
                  <span class="ray" data-ipucu="{Kacis($"{d.Etiket}\n{d.Deger.ToString("N0", Tr)} talep")}">
                    <b style="width:{S(oran)}%"></b>
                  </span>
                  <span class="sayi">{d.Deger.ToString("N0", Tr)}{adet}</span>
                </li>
                """);
        }
        sb.Append("</ul>");
        return new HtmlString(sb.ToString());
    }

    // ---------------------------------------------------------------- yardimci
    private static IHtmlContent Bos(string metin) => new HtmlString($"""
        <p style="margin:0;padding:34px 0;text-align:center;color:var(--muted-2);font-size:13px">{Kacis(metin)}</p>
        """);

    /// <summary>Eksenin okunur bir yuvarlak tavana cikmasi icin.</summary>
    private static double Tavan(int enb)
    {
        if (enb <= 4) return 4;
        var basamak = Math.Pow(10, Math.Floor(Math.Log10(enb)));
        foreach (var kat in new[] { 1.0, 2.0, 2.5, 5.0, 10.0 })
        {
            var t = basamak * kat;
            if (t >= enb) return Math.Ceiling(t / 4) * 4;
        }
        return Math.Ceiling(enb / 4.0) * 4;
    }
}
