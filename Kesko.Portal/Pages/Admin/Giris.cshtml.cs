using System.Security.Claims;
using Kesko.Portal.Data;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kesko.Portal.Pages.Admin;

[AllowAnonymous]
public class GirisModel : PageModel
{
    private readonly AdminAuthService _auth;
    private readonly KayitService _kayit;

    public GirisModel(AdminAuthService auth, KayitService kayit)
    {
        _auth = auth;
        _kayit = kayit;
    }

    [BindProperty] public string? Kullanici { get; set; }
    [BindProperty] public string? Sifre { get; set; }
    [BindProperty] public bool BeniHatirla { get; set; }

    public string? Hata { get; private set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/admin/gosterge");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var ip = KayitService.Ip(HttpContext);

        if (_auth.KilitliMi(ip, out var kalan))
        {
            Hata = $"Çok fazla hatalı deneme. {TimeSpan.FromSeconds(kalan):mm\\:ss} sonra tekrar deneyin.";
            return Page();
        }

        if (!_auth.Dogrula(Kullanici, Sifre, ip))
        {
            await _kayit.YazAsync(KayitTur.BasarisizGiris,
                $"Hatalı giriş denemesi. Girilen kullanıcı adı: {Kisalt(Kullanici)}",
                Kisalt(Kullanici));
            Hata = "Kullanıcı adı veya şifre hatalı.";
            return Page();
        }

        var kimlik = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, _auth.KullaniciAdi),
            new Claim("ad", _auth.GorunenAd),
            new Claim(ClaimTypes.Role, "Yonetici")
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(kimlik),
            new AuthenticationProperties
            {
                IsPersistent = BeniHatirla,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(BeniHatirla ? 24 * 14 : 8)
            });

        await _kayit.YazAsync(KayitTur.Giris, "Panele giriş yapıldı.", _auth.KullaniciAdi);

        // Acik yonlendirme olmasin: yalnizca kendi sitemizdeki yollar.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect("/admin/gosterge");
    }

    private static string Kisalt(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "—" : (s.Length > 60 ? s[..60] : s);
}
