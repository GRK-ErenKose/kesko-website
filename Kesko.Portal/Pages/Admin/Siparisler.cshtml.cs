using Kesko.Portal.Data;
using Kesko.Portal.Services;

namespace Kesko.Portal.Pages.Admin;

public class SiparislerModel : TalepListeBase
{
    public SiparislerModel(AppDbContext db, TalepService talepler, KayitService kayit)
        : base(db, talepler, kayit) { }

    public override TalepTur Tur => TalepTur.Siparis;
    public override string Kok => "siparis";
    public override string ListeYolu => "/admin/siparisler";
}
