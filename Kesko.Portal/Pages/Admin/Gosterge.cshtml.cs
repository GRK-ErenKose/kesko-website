using Kesko.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kesko.Portal.Pages.Admin;

public class GostergeModel : PageModel
{
    private readonly RaporService _rapor;
    public GostergeModel(RaporService rapor) => _rapor = rapor;

    public GostergeVerisi V { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        V = await _rapor.GostergeAsync();
        ViewData["Baslik"] = "Gösterge paneli";
        ViewData["Yol"] = "Genel bakış";
    }
}
