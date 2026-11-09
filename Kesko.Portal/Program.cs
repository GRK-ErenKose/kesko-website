using Kesko.Portal.Api;
using Kesko.Portal.Data;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.WebEncoders;
using System.Text.Encodings.Web;
using System.Text.Unicode;

// Yardimci komut: panel sifresini degistirmek icin yeni ozet uretir.
//   dotnet run -- sifre-ozeti "YeniSifre"
// Cikti appsettings icindeki Admin:PasswordHash alanina yazilir.
if (args is ["sifre-ozeti", var yeniSifre, ..])
{
    Console.WriteLine(AdminAuthService.OzetUret(yeniSifre));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Yerel sirlar (panel kullanici adi/sifresi) surum kontrolune girmeyen
// ayri bir dosyada durur; appsettings.json ve ortam dosyasindan sonra
// yuklendigi icin onlari ezer. Yayinda bu dosya yerine ortam
// degiskenleri de kullanilabilir: Admin__Username, Admin__PasswordHash
builder.Configuration.AddJsonFile("appsettings.Local.json",
    optional: true, reloadOnChange: true);

// --- veritabani -------------------------------------------------------------
// SQLite: tek dosya, ayri sunucu gerektirmez. Dosya App_Data altinda durur.
var veriDizin = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(veriDizin);
// Yapilandirmada bos birakilmissa App_Data altindaki varsayilan dosya kullanilir.
// (Bos dizge geri dondurulurse SQLite her baglanti icin ayri bir gecici
// veritabani acar; bu yuzden bosluk kontrolu sart.)
var yapilandirilmis = builder.Configuration.GetConnectionString("Default");
var baglanti = string.IsNullOrWhiteSpace(yapilandirilmis)
    ? $"Data Source={Path.Combine(veriDizin, "kesko.db")}"
    : yapilandirilmis;

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(baglanti));

// DataProtection anahtarlari diske yazilir; aksi halde uygulama her
// yeniden baslatmada SMTP sifresini cozemez hale gelir.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(veriDizin, "keys")))
    .SetApplicationName("Kesko.Portal");

// --- servisler --------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AdminAuthService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<TalepService>();
builder.Services.AddScoped<RaporService>();
builder.Services.AddScoped<KayitService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "kesko.panel";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/admin/giris";
        o.LogoutPath = "/admin/cikis";
        o.AccessDeniedPath = "/admin/giris";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Panel", p => p.RequireAuthenticatedUser());
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", "Panel");
    o.Conventions.AllowAnonymousToPage("/Admin/Giris");
});

// Razon varsayilani ASCII disi her karakteri sayisal varlik olarak kacirir
// (&#x15F; gibi). Turkce harfleri oldugu gibi yazdirmak hem sayfa boyutunu
// kucultur hem de ciktiyi okunur kilar.
builder.Services.Configure<WebEncoderOptions>(o =>
    o.TextEncoderSettings = new TextEncoderSettings(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Latin1Supplement,
        UnicodeRanges.LatinExtendedA,
        UnicodeRanges.GeneralPunctuation));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // IIS / nginx arkasinda calisirken istemci IP'si dogru okunsun.
    // Yalnizca bilinen vekiller eklenirse burayi daraltin.
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// --- veritabanini hazirla ---------------------------------------------------
using (var kapsam = app.Services.CreateScope())
{
    var db = kapsam.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.HazirlaAsync();
}

// --- boru hatti -------------------------------------------------------------
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["X-Frame-Options"] = "SAMEORIGIN";
    h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

// Panel varliklari wwwroot altinda.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Panel CSS/JS'i surum etiketiyle isteniyor; uzun onbellek guvenli.
        if (ctx.File.Name.EndsWith(".css") || ctx.File.Name.EndsWith(".js"))
            ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});

// Kurumsal site bir ust dizinde duruyor ve oradan sunuluyor: dosyalar
// kopyalanmadigi icin site ile panel tek kaynaktan calisir.
// Yayinda "SitePath" bos birakilirsa yalnizca wwwroot kullanilir.
var siteDizin = builder.Configuration["SitePath"] ?? "..";
var siteTam = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, siteDizin));
if (File.Exists(Path.Combine(siteTam, "index.html")))
{
    var saglayici = new PhysicalFileProvider(siteTam);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = saglayici,
        DefaultFileNames = ["index.html"]
    });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = saglayici });
    app.Logger.LogInformation("Kurumsal site şu dizinden sunuluyor: {Dizin}", siteTam);
}
else
{
    app.Logger.LogWarning(
        "index.html bulunamadı ({Dizin}). Kurumsal site sunulmayacak; panel çalışmaya devam eder.", siteTam);
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapFormEndpoints();
app.MapRazorPages();

app.MapGet("/admin", () => Results.Redirect("/admin/gosterge"));
app.MapGet("/hata", () => Results.Problem("Beklenmeyen bir hata oluştu."));

app.Run();
