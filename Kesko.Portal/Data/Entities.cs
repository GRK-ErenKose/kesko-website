using System.ComponentModel.DataAnnotations;

namespace Kesko.Portal.Data;

/// <summary>
/// Talep yasam dongusu. Panelde durum filtresi ve rozet rengi buna gore.
/// </summary>
public enum TalepDurum
{
    Yeni = 0,
    Incelemede = 1,
    TeklifVerildi = 2,
    Kazanildi = 3,
    Kaybedildi = 4,
    Spam = 5
}

public enum TalepTur
{
    Siparis = 0,
    Teklif = 1
}

/// <summary>
/// Siparis ve teklif taleplerinin ortak govdesi. Iki form da ayni tabloya
/// dusuyor; tipe ozel alanlar nullable olarak duruyor. Tek tabloda tutmak
/// gosterge panelinde ve raporda birlestirme yapmayi gereksiz kiliyor.
/// </summary>
public class Talep
{
    public int Id { get; set; }

    public TalepTur Tur { get; set; }
    public TalepDurum Durum { get; set; } = TalepDurum.Yeni;

    /// <summary>Panelde gosterilen kisa referans: KSK-2611-0042</summary>
    [MaxLength(24)]
    public string Referans { get; set; } = "";

    // --- ortak iletisim alanlari ---
    [MaxLength(160)] public string? Firma { get; set; }
    [MaxLength(120)] public string? Yetkili { get; set; }
    [MaxLength(160)] public string? Eposta { get; set; }
    [MaxLength(40)] public string? Telefon { get; set; }
    [MaxLength(120)] public string? Sehir { get; set; }

    // --- siparis formu alanlari ---
    [MaxLength(60)] public string? Urun { get; set; }
    [MaxLength(40)] public string? Gramaj { get; set; }
    public int? Adet { get; set; }
    /// <summary>"ic" = Ankara ici, "dis" = Ankara disi.</summary>
    [MaxLength(10)] public string? Bolge { get; set; }
    public bool KargoBedava { get; set; }

    // --- teklif formu alanlari ---
    [MaxLength(80)] public string? Hat { get; set; }
    public string? Mesaj { get; set; }

    // --- teknik iz ---
    public DateTime OlusturmaUtc { get; set; } = DateTime.UtcNow;
    public DateTime? GuncellemeUtc { get; set; }
    [MaxLength(64)] public string? Ip { get; set; }
    [MaxLength(400)] public string? UserAgent { get; set; }
    [MaxLength(400)] public string? Referrer { get; set; }
    /// <summary>Bildirim e-postasi SMTP uzerinden gitti mi.</summary>
    public bool BildirimGonderildi { get; set; }
    [MaxLength(400)] public string? BildirimHata { get; set; }

    public List<TalepNot> Notlar { get; set; } = new();

    // --- turetilmis gorunum yardimcilari ---
    public string DurumEtiket => Durum switch
    {
        TalepDurum.Yeni => "Yeni",
        TalepDurum.Incelemede => "İncelemede",
        TalepDurum.TeklifVerildi => "Teklif verildi",
        TalepDurum.Kazanildi => "Kazanıldı",
        TalepDurum.Kaybedildi => "Kaybedildi",
        _ => "Spam"
    };

    public string DurumSinif => Durum switch
    {
        TalepDurum.Yeni => "s-yeni",
        TalepDurum.Incelemede => "s-inceleme",
        TalepDurum.TeklifVerildi => "s-teklif",
        TalepDurum.Kazanildi => "s-kazanildi",
        TalepDurum.Kaybedildi => "s-kaybedildi",
        _ => "s-spam"
    };

    public string BolgeEtiket => Bolge switch
    {
        "ic" => "Ankara içi",
        "dis" => "Ankara dışı",
        _ => "—"
    };

    public string Baslik => !string.IsNullOrWhiteSpace(Firma)
        ? Firma!
        : !string.IsNullOrWhiteSpace(Yetkili) ? Yetkili! : (Eposta ?? "İsimsiz talep");

    /// <summary>
    /// tel: baglantisi icin cevirilebilir bicim. Telefon ekranda maskeli
    /// gorunur ("0 (532) 123 45 67"); baglanti ise bosluksuz olmali.
    /// </summary>
    public string? TelefonHref
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Telefon)) return null;

            var d = new string(Telefon.Where(char.IsDigit).ToArray());
            if (d.Length == 0) return null;

            if (d.StartsWith("0090")) d = d[4..];
            else if (d.StartsWith("90") && d.Length > 10) d = d[2..];
            if (d.StartsWith('0')) d = d[1..];

            // On haneli numara Turkiye kabul edilir; digerleri girildigi
            // ulke koduyla birlikte gelir.
            return d.Length == 10 ? "+90" + d : "+" + d;
        }
    }
}

public class TalepNot
{
    public int Id { get; set; }
    public int TalepId { get; set; }
    public Talep? Talep { get; set; }
    public string Metin { get; set; } = "";
    [MaxLength(80)] public string Yazan { get; set; } = "";
    public DateTime OlusturmaUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Panelden duzenlenen SMTP ve bildirim ayarlari. Tek satir tutulur (Id = 1).
/// Sifre, DataProtection ile sifrelenmis halde saklanir.
/// </summary>
public class SmtpAyar
{
    public int Id { get; set; }
    [MaxLength(200)] public string Sunucu { get; set; } = "";
    public int Port { get; set; } = 587;
    /// <summary>none | starttls | ssl</summary>
    [MaxLength(20)] public string Guvenlik { get; set; } = "starttls";
    [MaxLength(200)] public string KullaniciAdi { get; set; } = "";
    /// <summary>DataProtection ile korunmus sifre. Panelde asla geri gosterilmez.</summary>
    public string? SifreKorumali { get; set; }
    [MaxLength(200)] public string GonderenAdres { get; set; } = "";
    [MaxLength(120)] public string GonderenAd { get; set; } = "Kesko Web";
    /// <summary>Bildirimin gidecegi adresler; virgul ile ayrilir.</summary>
    [MaxLength(600)] public string AliciAdresler { get; set; } = "";
    [MaxLength(200)] public string? YanitAdresi { get; set; }

    public bool BildirimAktif { get; set; } = true;
    /// <summary>Form sahibine otomatik tesekkur e-postasi gonder.</summary>
    public bool OtomatikYanitAktif { get; set; }
    public string? OtomatikYanitKonu { get; set; } = "Talebiniz bize ulaştı — Kesko";
    public string? OtomatikYanitGovde { get; set; }

    public DateTime? SonTestUtc { get; set; }
    [MaxLength(400)] public string? SonTestSonuc { get; set; }
    public bool SonTestBasarili { get; set; }
}

/// <summary>
/// Statik sitenin panelden yonetilen sabitleri. main.js bunlari
/// /api/site-ayar ucundan okur; uc erisilemezse kendi varsayilanini kullanir.
/// </summary>
public class SiteAyar
{
    public int Id { get; set; }
    public int KargoBedavaEsik { get; set; } = 200;
    [MaxLength(40)] public string WhatsappNumara { get; set; } = "900000000000";
    [MaxLength(200)] public string IletisimEposta { get; set; } = "merhaba@kesko.com.tr";
    [MaxLength(40)] public string IletisimTelefon { get; set; } = "+90 (000) 000 00 00";
    /// <summary>Form gonderimlerini gecici olarak kapatmak icin.</summary>
    public bool FormlarAktif { get; set; } = true;
    [MaxLength(300)] public string? BakimMesaji { get; set; }
}

public enum KayitTur
{
    Giris = 0,
    BasarisizGiris = 1,
    Cikis = 2,
    DurumDegisti = 3,
    NotEklendi = 4,
    AyarDegisti = 5,
    TestMaili = 6,
    Disaaktarim = 7,
    TalepSilindi = 8
}

/// <summary>Panel hareketlerinin denetim izi.</summary>
public class Kayit
{
    public int Id { get; set; }
    public KayitTur Tur { get; set; }
    [MaxLength(80)] public string Kullanici { get; set; } = "";
    [MaxLength(400)] public string Aciklama { get; set; } = "";
    [MaxLength(64)] public string? Ip { get; set; }
    public DateTime OlusturmaUtc { get; set; } = DateTime.UtcNow;

    public string TurEtiket => Tur switch
    {
        KayitTur.Giris => "Giriş",
        KayitTur.BasarisizGiris => "Başarısız giriş",
        KayitTur.Cikis => "Çıkış",
        KayitTur.DurumDegisti => "Durum değişti",
        KayitTur.NotEklendi => "Not eklendi",
        KayitTur.AyarDegisti => "Ayar değişti",
        KayitTur.TestMaili => "Test e-postası",
        KayitTur.Disaaktarim => "Dışa aktarım",
        _ => "Talep silindi"
    };
}
