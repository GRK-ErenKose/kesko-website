using Microsoft.EntityFrameworkCore;

namespace Kesko.Portal.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Talep> Talepler => Set<Talep>();
    public DbSet<TalepNot> TalepNotlari => Set<TalepNot>();
    public DbSet<SmtpAyar> SmtpAyarlari => Set<SmtpAyar>();
    public DbSet<SiteAyar> SiteAyarlari => Set<SiteAyar>();
    public DbSet<Kayit> Kayitlar => Set<Kayit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Talep>(e =>
        {
            e.HasIndex(x => x.OlusturmaUtc);
            e.HasIndex(x => new { x.Tur, x.Durum });
            e.HasIndex(x => x.Referans).IsUnique();
            e.HasMany(x => x.Notlar)
             .WithOne(x => x.Talep!)
             .HasForeignKey(x => x.TalepId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Kayit>().HasIndex(x => x.OlusturmaUtc);

        base.OnModelCreating(b);
    }

    /// <summary>
    /// Semayi kurar ve tek satirli ayar tablolarini doldurur. Sema sabit
    /// oldugu icin migration yerine EnsureCreated yeterli.
    /// </summary>
    public async Task HazirlaAsync()
    {
        await Database.EnsureCreatedAsync();

        if (!await SmtpAyarlari.AnyAsync())
        {
            SmtpAyarlari.Add(new SmtpAyar
            {
                Id = 1,
                Sunucu = "",
                Port = 587,
                Guvenlik = "starttls",
                GonderenAd = "Kesko Web",
                AliciAdresler = "merhaba@kesko.com.tr",
                BildirimAktif = false,
                OtomatikYanitGovde =
                    "Merhaba {Yetkili},\n\n" +
                    "Talebiniz bize ulaştı. Ekibimiz en kısa sürede size dönecek.\n" +
                    "Referans numaranız: {Referans}\n\n" +
                    "Kesko Gıda\nBi'Dilim bir KESKO markasıdır."
            });
        }

        if (!await SiteAyarlari.AnyAsync())
            SiteAyarlari.Add(new SiteAyar { Id = 1 });

        await SaveChangesAsync();
    }

    /// <summary>KSK-YYAA-SIRA bicimli referans uretir.</summary>
    public async Task<string> SonrakiReferansAsync()
    {
        var now = DateTime.UtcNow;
        var onek = $"KSK-{now:yyMM}-";
        var son = await Talepler
            .Where(t => t.Referans.StartsWith(onek))
            .OrderByDescending(t => t.Id)
            .Select(t => t.Referans)
            .FirstOrDefaultAsync();

        var sira = 1;
        if (son is not null && int.TryParse(son[onek.Length..], out var n))
            sira = n + 1;

        return onek + sira.ToString("0000");
    }
}
