using Microsoft.AspNetCore.Html;

namespace Kesko.Portal.Models;

/// <summary>
/// Panelde kullanilan tum simgeler. Tek cizgi kalinligi (1.8) ve 24x24
/// kutu ile cizildi; renk currentColor'dan gelir. Harici simge paketi
/// yuklenmemesi icin govdeler burada duruyor.
/// </summary>
public static class Ikon
{
    private static readonly Dictionary<string, string> Yollar = new()
    {
        ["gosterge"] = "M4 13h6V4H4zM14 20h6v-9h-6zM4 20h6v-4H4zM14 8h6V4h-6z",
        ["siparis"] = "M3 6h13v9H3zM16 9h3.5l2.5 2.6V15h-6M6.5 19a1.6 1.6 0 1 0 0-3.2 1.6 1.6 0 0 0 0 3.2M17.5 19a1.6 1.6 0 1 0 0-3.2 1.6 1.6 0 0 0 0 3.2",
        ["teklif"] = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M9 13h6M9 17h4",
        ["rapor"] = "M4 20V10M10 20V4M16 20v-7M22 20H2",
        ["ayar"] = "M12 15.2a3.2 3.2 0 1 0 0-6.4 3.2 3.2 0 0 0 0 6.4M19.4 15a1.7 1.7 0 0 0 .34 1.87l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.7 1.7 0 0 0-1.87-.34 1.7 1.7 0 0 0-1.03 1.56V21a2 2 0 1 1-4 0v-.08A1.7 1.7 0 0 0 8.9 19.3a1.7 1.7 0 0 0-1.87.34l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.7 1.7 0 0 0 .34-1.87 1.7 1.7 0 0 0-1.56-1.03H3a2 2 0 1 1 0-4h.08A1.7 1.7 0 0 0 4.7 8.9a1.7 1.7 0 0 0-.34-1.87l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.7 1.7 0 0 0 1.87.34H9a1.7 1.7 0 0 0 1.03-1.56V3a2 2 0 1 1 4 0v.08a1.7 1.7 0 0 0 1.03 1.56 1.7 1.7 0 0 0 1.87-.34l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.7 1.7 0 0 0-.34 1.87V9a1.7 1.7 0 0 0 1.56 1.03H21a2 2 0 1 1 0 4h-.08A1.7 1.7 0 0 0 19.4 15",
        ["kayit"] = "M12 8v4l3 2M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18",
        ["mail"] = "M3 7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2zM3.5 7.5 12 13l8.5-5.5",
        ["ara"] = "M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14M20 20l-4.2-4.2",
        ["dis"] = "M15 3h6v6M21 3l-9 9M19 14v5a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h5",
        ["indir"] = "M12 4v11M7.5 10.5 12 15l4.5-4.5M4 19h16",
        ["yukari"] = "m6 14 6-6 6 6",
        ["asagi"] = "m6 10 6 6 6-6",
        ["sirala"] = "m7 10 5-5 5 5M7 14l5 5 5-5",
        ["sol"] = "m14 6-6 6 6 6",
        ["sag"] = "m10 6 6 6-6 6",
        ["kapat"] = "M6 6l12 12M18 6 6 18",
        ["menu"] = "M4 7h16M4 12h16M4 17h16",
        ["gunes"] = "M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10M12 2v2M12 20v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M2 12h2M20 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4",
        ["ay"] = "M20 14.5A8.5 8.5 0 0 1 9.5 4a8.5 8.5 0 1 0 10.5 10.5",
        ["cikis"] = "M15 17l5-5-5-5M20 12H9M12 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h6",
        ["kisi"] = "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8M5 21a7 7 0 0 1 14 0",
        ["kutu"] = "M4 8.5 12 4l8 4.5v7L12 20l-8-4.5zM4 8.5 12 13l8-4.5M12 13v7",
        ["konum"] = "M12 21s7-5.5 7-11a7 7 0 1 0-14 0c0 5.5 7 11 7 11M12 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5",
        ["telefon"] = "M7.5 3h-2A2.5 2.5 0 0 0 3 5.6C3 13 11 21 18.4 21A2.5 2.5 0 0 0 21 18.5v-2l-4.5-2-2 2.5a14 14 0 0 1-5.5-5.5L11.5 9z",
        ["not"] = "M4 5a2 2 0 0 1 2-2h8l6 6v10a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zM14 3v6h6M8 14h8M8 17h5",
        ["ok"] = "M20 6 9 17l-5-5",
        ["uyari"] = "M12 9v5M12 17.2v.3M10.3 3.9 2.5 17.5A1.7 1.7 0 0 0 4 20h16a1.7 1.7 0 0 0 1.5-2.5L13.7 3.9a1.7 1.7 0 0 0-3 0",
        ["bilgi"] = "M12 11v5M12 7.5v.3M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18",
        ["yenile"] = "M20 12a8 8 0 1 1-2.6-5.9M20 4v4h-4",
        ["filtre"] = "M3 5h18l-7 8v6l-4 2v-8z",
        ["yazdir"] = "M7 8V3h10v5M7 18H5a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-2M7 14h10v7H7z",
        ["takvim"] = "M4 7a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zM4 10h16M8 3v4M16 3v4",
        ["kopya"] = "M9 9h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2M15 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2",
        ["cop"] = "M4 7h16M9 7V4h6v3M6 7l1 14h10l1-14M10 11v6M14 11v6",
        ["sunucu"] = "M4 5a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zM4 15a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zM7.5 6v.3M7.5 16v.3",
        ["kilit"] = "M6 11h12a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1M8 11V7.5a4 4 0 0 1 8 0V11",
        ["goz"] = "M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6",
        ["gozkapali"] = "M4 4l16 16M9.9 5.8A8.9 8.9 0 0 1 12 5.5c6 0 9.5 6.5 9.5 6.5a17 17 0 0 1-2.4 3.3M6.3 7.9A17 17 0 0 0 2.5 12S6 18.5 12 18.5c.9 0 1.7-.1 2.5-.4M10 10a3 3 0 0 0 4 4",
        ["gonder"] = "M21 3 3 10.5l7 2.5 2.5 7z",
        ["bos-kutu"] = "M4 8.5 12 4l8 4.5v7L12 20l-8-4.5zM4 8.5 12 13l8-4.5M12 13v7",
        ["grafik"] = "M4 4v16h16M8 16v-4M12 16V8M16 16v-6"
    };

    public static IHtmlContent Ciz(string ad, int boyut = 24, string? sinif = null)
    {
        if (!Yollar.TryGetValue(ad, out var yol))
            return HtmlString.Empty;

        var s = sinif is null ? "" : $" class=\"{sinif}\"";
        return new HtmlString(
            $"<svg{s} width=\"{boyut}\" height=\"{boyut}\" viewBox=\"0 0 24 24\" fill=\"none\" " +
            $"stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" " +
            $"stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"{yol}\"/></svg>");
    }
}
