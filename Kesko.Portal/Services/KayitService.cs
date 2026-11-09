using Kesko.Portal.Data;

namespace Kesko.Portal.Services;

/// <summary>Denetim izi yazar. Panel hareketleri buradan gecer.</summary>
public class KayitService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _ctx;

    public KayitService(AppDbContext db, IHttpContextAccessor ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task YazAsync(KayitTur tur, string aciklama, string? kullanici = null)
    {
        var http = _ctx.HttpContext;
        _db.Kayitlar.Add(new Kayit
        {
            Tur = tur,
            Aciklama = aciklama.Length > 400 ? aciklama[..400] : aciklama,
            Kullanici = kullanici ?? http?.User.Identity?.Name ?? "—",
            Ip = Ip(http)
        });
        await _db.SaveChangesAsync();
    }

    public static string Ip(HttpContext? http)
    {
        if (http is null) return "—";
        // Ters vekil arkasinda calisiyorsa ForwardedHeaders middleware'i
        // RemoteIpAddress'i zaten duzeltmis olur.
        return http.Connection.RemoteIpAddress?.ToString() ?? "—";
    }
}
