# Kesko Portal — yönetim paneli ve form arka ucu

ASP.NET Core 10 (Razor Pages) uygulaması. Üç işi birlikte yapar:

1. **Kurumsal siteyi sunar** — bir üst klasördeki `index.html` ve `assets/`
   olduğu yerden servis edilir, kopyalanmaz.
2. **Form arka ucu** — sipariş hattı ve toplu tedarik formları artık
   `mailto:` yerine gerçek bir uca POST eder, veritabanına yazılır,
   SMTP ile bildirim gider.
3. **Yönetim paneli** — `/admin` altında talep yönetimi, filtreler,
   raporlar, SMTP ayarları ve işlem kayıtları.

## Çalıştırma

```bash
cd Kesko.Portal
dotnet run
```

Site `http://localhost:5098`, panel `http://localhost:5098/admin`.

İlk açılışta veritabanı ve tek satırlı ayar kayıtları oluşturulur; başka
hiçbir veri yazılmaz. Paneldeki her kayıt sitedeki formlardan gelir.

## Panel girişi

> **Depoyu yeni klonladıysanız panele giriş yapılamaz.** Kullanıcı adı ve
> şifre bilinçli olarak depoda tutulmuyor. Aşağıdaki dosyayı oluşturun.

`Kesko.Portal/appsettings.Local.json` — bu dosya `.gitignore` ile dışlanmıştır:

```json
{
  "Admin": {
    "Username": "kullanici.adiniz",
    "PasswordHash": "210000.<saltBase64>.<hashBase64>"
  }
}
```

Özeti üretmek için:

```bash
cd Kesko.Portal
dotnet run -- sifre-ozeti "SeçtiğinizGüçlüŞifre"
```

Çıkan satırı `PasswordHash` alanına yazın. Şifre hiçbir yerde düz metin
tutulmaz; PBKDF2-SHA256 özeti olarak saklanır, karşılaştırma sabit zamanlı
yapılır, beş hatalı denemeden sonra IP 15 dakika kilitlenir.

Yayın ortamında dosya yerine ortam değişkeni de kullanılabilir:

```
Admin__Username=...
Admin__PasswordHash=...
```

Yükleme sırası: `appsettings.json` → `appsettings.{Ortam}.json` →
`appsettings.Local.json` → ortam değişkenleri. Sonraki öncekini ezer.

## Dosya yapısı

```
Kesko.Portal/
├── Program.cs                 boru hattı, kimlik doğrulama, statik dosyalar
├── appsettings.json           Admin hesabı, SitePath, bağlantı dizesi
├── Api/FormEndpoints.cs       POST /api/siparis, /api/teklif, GET /api/site-ayar
├── Data/
│   ├── Entities.cs            Talep, TalepNot, SmtpAyar, SiteAyar, Kayit
│   └── AppDbContext.cs        şema + referans üretimi (KSK-YYAA-SIRA)
├── Services/
│   ├── AdminAuthService.cs    PBKDF2 doğrulama, kaba kuvvet kilidi
│   ├── MailService.cs         MailKit ile SMTP, şablonlar, test gönderimi
│   ├── TalepService.cs        filtre/sayfalama/CSV — tek kaynak
│   ├── RaporService.cs        gösterge ve rapor toplamları
│   ├── Grafik.cs              SVG grafik üretimi (harici kütüphane yok)
│   └── KayitService.cs        denetim izi
├── Models/
│   ├── TalepFiltre.cs         querystring sözleşmesi
│   └── Ikon.cs                tüm SVG simgeler
├── Pages/Admin/               panel sayfaları
└── wwwroot/admin/             panelin CSS ve JS dosyaları
```

## Panel sayfaları

| Yol | İçerik |
|---|---|
| `/admin/giris` | giriş |
| `/admin/gosterge` | KPI'lar, 30 günlük akış, dağılımlar, son talepler |
| `/admin/siparisler` | sipariş talepleri: filtre, sıralama, toplu durum, CSV |
| `/admin/teklifler` | teklif talepleri: aynı yetenekler, hat kırılımı |
| `/admin/siparis/{id}` · `/admin/teklif/{id}` | tek talep: özet, teknik iz, e-posta ile yanıt, notlar |
| `/admin/raporlar` | tarih aralığı, önceki dönemle karşılaştırma, saat/gün dağılımı, en aktif firmalar |
| `/admin/ayarlar` | SMTP, otomatik yanıt, site sabitleri |
| `/admin/kayitlar` | işlem kayıtları |

## Veritabanı

SQLite, `App_Data/kesko.db`. Ayrı sunucu gerekmez. Şema sabit olduğu için
migration yerine `EnsureCreated` kullanılır; uygulama ilk açılışta tabloları
ve tek satırlı ayar kayıtlarını oluşturur.

SQL Server'a geçmek isterseniz `appsettings.json` içindeki
`ConnectionStrings:Default` alanını doldurup `Program.cs` içindeki
`UseSqlite` çağrısını `UseSqlServer` ile değiştirmek yeterli; model
değişmiyor.

**`App_Data` yedeklenmeli ve sürüm kontrolüne girmemeli.** İçinde hem
müşteri verisi hem de SMTP şifresini çözen DataProtection anahtarları var.

## SMTP

Ayarlar veritabanında tutulur, panelden düzenlenir. Şifre DataProtection
ile şifrelenip saklanır, ekranda bir daha gösterilmez.

- Bildirim kapalıyken talepler yine kaydedilir, yalnızca e-posta gitmez.
- Bildirim gönderilemezse talep kaybolmaz: hata talebin üzerine yazılır,
  panelde "bildirim gitmedi" etiketiyle ve göstergede uyarı olarak görünür.
- `Ayarlar` sayfasındaki test, kaydetmeden önce formdaki değerlerle
  tek bir e-posta gönderir.

DataProtection anahtarları değişirse (sunucu taşıma, `App_Data/keys`
silinmesi) kayıtlı şifre çözülemez; panel bunu "şifreyi yeniden girin"
diye bildirir.

## Form uçları

`POST /api/siparis`

```json
{ "urun": "Kakaolu Kek", "gramaj": "100 g", "adet": 450, "bolge": "dis",
  "sehir": "İzmir", "yetkili": "Ad Soyad", "firma": "Ege Market",
  "eposta": "ad@firma.com", "telefon": "0532…", "mesaj": "" }
```

`POST /api/teklif`

```json
{ "firma": "…", "yetkili": "…", "eposta": "…", "telefon": "…",
  "sehir": "…", "hat": "Private label", "gramaj": "İkisi de", "mesaj": "" }
```

Yanıt: `{ "ok": true, "mesaj": "…", "referans": "KSK-2611-0042" }`

Koruma katmanları:

- her alan sunucuda yeniden doğrulanır (istemci doğrulaması güvenlik değildir)
- gizli `website` alanı (bal küpü) doluysa istek sessizce yutulur
- IP başına 10 dakikada 5 gönderim; aşılırsa `429`
- `Ayarlar`dan formlar kapatılabilir; kapalıyken `503` ve bakım mesajı döner

`GET /api/site-ayar` kargo bedava eşiği, WhatsApp numarası ve iletişim
bilgilerini döndürür. `main.js` bunu okur, uca ulaşamazsa dosyadaki
varsayılanlarla devam eder — yani site sunucusuz da çalışmaya devam eder.

## Yayına alma

```bash
dotnet publish -c Release -o yayin
```

Siteyi de aynı sunucudan vermek için `index.html` ve `assets/` klasörünü
yayın klasörünün yanına koyup `SitePath` değerini oraya çevirin, ya da
`SitePath` değerini boş bırakıp dosyaları `wwwroot` altına kopyalayın.

Ters vekil (IIS, nginx) arkasında HTTPS sonlandırılıyorsa `ForwardedHeaders`
zaten açık; `KnownProxies` listesini daraltmanız önerilir.

## Tasarım

Panel, kurumsal sitenin tasarım dilini kullanır: aynı marka renkleri
(lacivert `#2B2971`, sarı `#F8E300`), aynı tipografi (Bricolage Grotesque +
Inter), aynı yuvarlak kenar ve ince çizgi mantığı.

- Tüm jetonlar `wwwroot/admin/css/admin.css` başındaki `:root` bloğunda.
  Marka rengi değişecekse yalnızca orası düzenlenir.
- Koyu tema `data-tema="koyu"` ile çalışır; seçim `localStorage`'da tutulur,
  ilk açılışta işletim sistemi tercihine uyar.
- Grafikler el ile SVG olarak üretilir (`Services/Grafik.cs`). Harici grafik
  kütüphanesi yok; çıktı koyu temada ve yazdırmada bozulmaz.
- Simgeler `Models/Ikon.cs` içinde; simge paketi indirilmez.
- CSS ve JS dışında bağımlılık yoktur: Bootstrap, jQuery, Chart.js yok.
- `prefers-reduced-motion` açık kullanıcılarda animasyonlar durur;
  yazdırma için ayrı düzen tanımlı.
