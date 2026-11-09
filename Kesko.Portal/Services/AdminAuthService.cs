using System.Security.Cryptography;
using System.Text;

namespace Kesko.Portal.Services;

public class AdminHesapAyar
{
    /// <summary>Sabit yonetici kullanici adi.</summary>
    public string Username { get; set; } = "";
    /// <summary>PBKDF2-SHA256 ciktisi: "yineleme.saltBase64.hashBase64".</summary>
    public string PasswordHash { get; set; } = "";
    public string GorunenAd { get; set; } = "Yönetici";
    /// <summary>Kac basarisiz denemeden sonra IP kilitlenir.</summary>
    public int MaksDeneme { get; set; } = 5;
    /// <summary>Kilit suresi (dakika).</summary>
    public int KilitDakika { get; set; } = 15;
}

/// <summary>
/// Tek kullanicili, sabit kimlikli dogrulama. Sifre yapilandirmada duz metin
/// olarak degil PBKDF2 ozeti olarak durur; karsilastirma sabit zamanli yapilir.
/// IP basina kaba kuvvet kilidi bellekte tutulur.
/// </summary>
public class AdminAuthService
{
    private readonly AdminHesapAyar _ayar;
    private readonly ILogger<AdminAuthService> _log;
    private readonly Dictionary<string, (int Sayac, DateTime? KilitBitis)> _denemeler = new();
    private readonly Lock _kilit = new();

    public AdminAuthService(IConfiguration cfg, ILogger<AdminAuthService> log)
    {
        _log = log;
        _ayar = cfg.GetSection("Admin").Get<AdminHesapAyar>() ?? new AdminHesapAyar();
        if (string.IsNullOrWhiteSpace(_ayar.Username) || string.IsNullOrWhiteSpace(_ayar.PasswordHash))
            _log.LogWarning("Admin:Username / Admin:PasswordHash tanimli degil. Panele giris yapilamaz.");
    }

    public string GorunenAd => _ayar.GorunenAd;
    public string KullaniciAdi => _ayar.Username;

    public bool KilitliMi(string ip, out int kalanSaniye)
    {
        kalanSaniye = 0;
        lock (_kilit)
        {
            if (!_denemeler.TryGetValue(ip, out var d) || d.KilitBitis is null) return false;
            var kalan = d.KilitBitis.Value - DateTime.UtcNow;
            if (kalan <= TimeSpan.Zero)
            {
                _denemeler.Remove(ip);
                return false;
            }
            kalanSaniye = (int)Math.Ceiling(kalan.TotalSeconds);
            return true;
        }
    }

    public bool Dogrula(string? kullanici, string? sifre, string ip)
    {
        if (string.IsNullOrEmpty(_ayar.PasswordHash)) return false;

        var kullaniciTamam = SabitZamanliEsit(kullanici ?? "", _ayar.Username);
        var sifreTamam = SifreDogrula(sifre ?? "", _ayar.PasswordHash);

        // Iki kontrol de her zaman kosuyor: kullanici adinin varligi
        // yanit suresinden anlasilmasin.
        if (kullaniciTamam && sifreTamam)
        {
            lock (_kilit) _denemeler.Remove(ip);
            return true;
        }

        lock (_kilit)
        {
            var d = _denemeler.TryGetValue(ip, out var v) ? v : (0, (DateTime?)null);
            var sayac = d.Item1 + 1;
            DateTime? bitis = sayac >= _ayar.MaksDeneme
                ? DateTime.UtcNow.AddMinutes(_ayar.KilitDakika)
                : null;
            _denemeler[ip] = (sayac, bitis);
        }
        return false;
    }

    private static bool SifreDogrula(string sifre, string kayitli)
    {
        var parca = kayitli.Split('.');
        if (parca.Length != 3) return false;
        if (!int.TryParse(parca[0], out var yineleme)) return false;

        try
        {
            var salt = Convert.FromBase64String(parca[1]);
            var beklenen = Convert.FromBase64String(parca[2]);
            var hesap = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(sifre), salt, yineleme,
                HashAlgorithmName.SHA256, beklenen.Length);
            return CryptographicOperations.FixedTimeEquals(hesap, beklenen);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool SabitZamanliEsit(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length)
        {
            // Uzunluk farkliysa yine de bir karsilastirma yap, sonra false don.
            CryptographicOperations.FixedTimeEquals(bb, bb);
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    /// <summary>
    /// Yeni bir sifre ozeti uretir. Sifre degistirilecekse bu ciktinin
    /// appsettings icindeki Admin:PasswordHash alanina yazilmasi gerekir.
    /// </summary>
    public static string OzetUret(string sifre, int yineleme = 210_000)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(sifre), salt, yineleme, HashAlgorithmName.SHA256, 32);
        return $"{yineleme}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }
}
