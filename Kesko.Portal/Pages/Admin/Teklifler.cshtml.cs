using Kesko.Portal.Data;
using Kesko.Portal.Services;

namespace Kesko.Portal.Pages.Admin;

public class TekliflerModel : TalepListeBase
{
    public TekliflerModel(AppDbContext db, TalepService talepler, KayitService kayit)
        : base(db, talepler, kayit) { }

    public override TalepTur Tur => TalepTur.Teklif;
    public override string Kok => "teklif";
    public override string ListeYolu => "/admin/teklifler";
}
