# Kesko — kurumsal site

Ön yüz statik: build adımı, paket yöneticisi, framework yok. Formların arka
ucu ve yönetim paneli `Kesko.Portal/` altındaki ASP.NET Core uygulamasıdır
([ayrıntılar](Kesko.Portal/README.md)).

Site, portal olmadan da açılır: form uçlarına ulaşamadığında kullanıcıya
e-posta ve WhatsApp seçeneğini gösterir, panelden yönetilen sabitler yerine
dosyadaki varsayılanları kullanır.

## Dosya yapısı

```
kesko-website/
├── index.html                          tek sayfa, bölümler id ile ayrılmış
├── assets/
│   ├── css/style.css                   tüm stiller
│   ├── js/main.js                      tüm etkileşimler
│   └── img/
│       ├── kesko-logo-sari.png         lacivert zemin üzerinde kullanılır
│       ├── kesko-logo-lacivert.png     beyaz zemin + favicon
│       ├── bidilim-logo.webp           Bi'Dilim kurdele logosu
│       ├── bidilim-kakaolu-kek.webp    arka planı silinmiş paket (şeffaf)
│       └── bidilim-mozaik-kek.webp     arka planı silinmiş paket (şeffaf)
├── Kesko.Portal/                       yönetim paneli + form arka ucu
└── README.md
```

## Yayına alma

İki seçenek var.

**Portal ile birlikte (önerilen).** Siteyi ve paneli aynı sunucu verir;
formlar veritabanına yazar, SMTP bildirimi gider. Windows + IIS ya da Linux
üzerinde .NET 10 çalışan bir ortam gerekir:

```bash
cd Kesko.Portal
dotnet run                 # site http://localhost:5098, panel /admin
```

**Yalnızca statik.** Netlify, Vercel, Cloudflare Pages, GitHub Pages ya da
klasik paylaşımlı hosting (`public_html` içine kopyalayın). Bu durumda
formlar çalışmaz; kullanıcıya e-posta/WhatsApp seçeneği gösterilir.

```bash
python3 -m http.server 8000     # sonra http://localhost:8000
```

## Teknik notlar

**Fontlar** Google Fonts'tan çekiliyor (`Bricolage Grotesque` + `Inter`).
İnternet erişimi olmayan bir ortam ya da KVKK/GDPR gerekçesiyle self-host
isteniyorsa fontları indirip `assets/fonts/` altına alın ve `style.css`
başındaki `@import`/`<link>` satırını `@font-face` ile değiştirin.

**Görseller** iki pakette WebP formatında. WebP tüm güncel tarayıcılarda
destekli. Çok eski tarayıcı desteği gerekiyorsa `<picture>` ile PNG fallback
eklenebilir.

**Erişilebilirlik** gözetildi: `aria-pressed`, `aria-expanded`, `aria-label`
nitelikleri yerinde, odak halkası (`:focus-visible`) tanımlı,
`prefers-reduced-motion` açık kullanıcılarda tüm animasyonlar duruyor.
Bu nitelikleri taşıyan elemanlar değiştirilirken JS tarafındaki seçicilerin
de güncellenmesi gerekir.

**Renk sistemi** `style.css` başındaki `:root` bloğunda. Marka rengi
değişecekse sadece orası düzenlenir, dosyanın geri kalanına dokunulmaz.

## Formlar

İki form da `fetch` ile sunucuya POST ediyor; sonuç sayfa içinde bildiriliyor.

| Form | Konum | Uç |
|---|---|---|
| Sipariş hattı | `#siparis` | `POST /api/siparis` |
| Toplu tedarik | `#tedarik` | `POST /api/teklif` |

Doğrulama iki yerde: tarayıcıda kullanıcı rahatlığı için, sunucuda gerçek
kontrol olarak. Spam koruması gizli `website` alanı (bal küpü) ve IP başına
hız sınırıyla sağlanıyor. Uca ulaşılamazsa hata mesajı e-posta ve WhatsApp
seçeneğini gösterir.

İlgili kod `main.js` içindeki `gonder` fonksiyonunda ve `o-send` / `send`
dinleyicilerinde. Alan sözleşmesi [portal README'sinde](Kesko.Portal/README.md).

## Panelden yönetilen sabitler

Aşağıdakiler artık `/admin/ayarlar` sayfasından değiştirilebiliyor.
Site bunları açılışta `GET /api/site-ayar` ile okuyor.

| Ne | Panel alanı | Dosyadaki varsayılan |
|---|---|---|
| Kargo bedava eşiği | Kargo bedava eşiği | `main.js` → `FREE = 200` |
| İletişim e-postası | İletişim e-postası | `merhaba@kesko.com.tr` |
| İletişim telefonu | İletişim telefonu | `+90 (000) 000 00 00` |
| WhatsApp numarası | WhatsApp numarası | `900000000000` |

Uca ulaşılamazsa (statik barındırma) dosyadaki varsayılanlar geçerli olur.
Bu nedenle `main.js` başındaki sabitleri de gerçek bilgiyle güncellemek
iyi olur:

```js
var FREE = 200, WA = '900000000000', MAIL = 'merhaba@kesko.com.tr';
```

Sitede panelden okunan değerler `data-site-eposta` ve `data-site-telefon`
nitelikleriyle işaretlenmiş. Yeni bir yere aynı bilgi konacaksa bu
nitelikleri eklemek yeterli.

**Hâlâ doldurulması gereken:** `index.html` `<head>` içindeki `og:url`,
`og:image` ve `canonical`.

## İçerik kararları (uygulandı)

Aşağıdakiler marka tarafından onaylanmış hâliyle koda işlendi.
Değiştirilmeden önce marka onayı alınmalı.

- **Gramaj:** 100 g ve 40 g dilim. Metinlerde, gramaj seçicisinde, sipariş
  hattında ve S.S.S.'te aynı değerler geçiyor.
- **Saklama:** Site artık sevkiyat ya da saklama sıcaklığı belirtmiyor.
  S.S.S.'teki tek ifade ambalaj arka yüzüyle birebir aynı: "serin ve kuru
  yerde, doğrudan güneş ışığından uzakta". Bu bilinçli bir karardır —
  sıcaklık değeri eklenmemeli, ambalajla çelişir.
- **Renkler:** KESKO laciverti `#2B2971`, KESKO sarısı `#F8E300`.
  `style.css` içindeki `:root` bloğunda tanımlı; logo PNG'leri de aynı
  değerlere boyandı.
- **Sloganlar:** Kakaolu Kek "Kakao aşkına", Mozaik Kek "Karar
  veremeyenlere", Limonlu Kek "Güneşi dilimleyenlere".
- **Kurumsal:** Raf ömrü 5 gün; footer'da "Bi'Dilim bir KESKO markasıdır"
  satırı ve üretici tüzel kişi bilgisi yer alıyor.

## Gramaj seçicisi hakkında

Ürün kartlarındaki 100 g / 40 g düğmesi görseli de ölçekliyor: 40 g
seçildiğinde paket görseli küçülüyor (`.media img.g40`, `--s:.82`).
Gramaj eklenecekse hem `main.js` içindeki `data-g` kontrolü hem de
`style.css` içindeki ölçek sınıfı birlikte güncellenmeli.
