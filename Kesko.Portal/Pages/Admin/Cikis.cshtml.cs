using Kesko.Portal.Data;
using Kesko.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kesko.Portal.Pages.Admin;

public class CikisModel : PageModel
{
    private readonly KayitService _kayit;
    public CikisModel(KayitService kayit) => _kayit = kayit;

    public IActionResult OnGet() => Redirect("/admin/gosterge");

    public async Task<IActionResult> OnPostAsync()
    {
        await _kayit.YazAsync(KayitTur.Cikis, "Panelden çıkış yapıldı.");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/admin/giris");
    }
}
