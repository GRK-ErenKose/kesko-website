<#
    Kesko - canliya alma betigi
    ===========================
    Kullanim:
        .\yayinla.ps1                 # sifreyi sorar
        .\yayinla.ps1 -Prova          # hicbir sey degistirmez, ne olacagini listeler

    NEDEN BU BETIK VAR
    ------------------
    "msdeploy -verb:sync" kaynakta bulunmayan dosyalari hedeften SILER.
    App_Data klasoru yayin paketinde yer almadigi icin, duz bir sync
    her yayinda sunucudaki su dosyalari yok eder:

        App_Data\kesko.db        -> tum talepler, notlar, SMTP ve site ayarlari
        App_Data\keys\*.xml      -> DataProtection anahtarlari

    Anahtarlar silinince acik oturumlar dusere, kayitli SMTP sifresi de
    bir daha cozulemez. Bu yuzden asagidaki -skip kurallari ZORUNLUDUR.
    Elle msdeploy calistirmayin; hep bu betigi kullanin.

    Sifre hicbir yere yazilmaz: ya KESKO_DEPLOY_PWD ortam degiskeninden
    okunur ya da calisma aninda sorulur.
#>

[CmdletBinding()]
param(
    # Yayin profili. Varsayilan: proje kokundeki .publishsettings dosyasi.
    [string]$ProfilDosyasi = (Join-Path $PSScriptRoot 'kesko.com.tr.publishsettings'),

    # Yalnizca ne olacagini gosterir, sunucuda hicbir degisiklik yapmaz.
    [switch]$Prova,

    # Yayin ciktisi klasoru.
    [string]$CikisKlasoru = (Join-Path $PSScriptRoot 'Kesko.Portal\yayin')
)

$ErrorActionPreference = 'Stop'

function Basamak($metin) { Write-Host "`n==> $metin" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1) profil
if (-not (Test-Path $ProfilDosyasi)) {
    throw "Yayin profili bulunamadi: $ProfilDosyasi"
}

[xml]$profil = Get-Content $ProfilDosyasi
$p = $profil.publishData.publishProfile
$bilgisayar = $p.publishUrl
$site       = $p.msdeploySite
$kullanici  = $p.userName

if (-not $bilgisayar.StartsWith('http')) { $bilgisayar = "https://$bilgisayar" }

Basamak "Hedef"
Write-Host "  Sunucu    : $bilgisayar"
Write-Host "  Site      : $site"
Write-Host "  Kullanici : $kullanici"

# ---------------------------------------------------------------- 2) msdeploy
$msdeploy = @(
    "$env:ProgramFiles\IIS\Microsoft Web Deploy V3\msdeploy.exe",
    "${env:ProgramFiles(x86)}\IIS\Microsoft Web Deploy V3\msdeploy.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $msdeploy) {
    throw "msdeploy.exe bulunamadi. Web Deploy 3.6 kurulu olmali: https://aka.ms/webdeploy"
}

# ---------------------------------------------------------------- 3) sifre
$sifre = $env:KESKO_DEPLOY_PWD
if ([string]::IsNullOrWhiteSpace($sifre)) {
    $gizli = Read-Host "Web Deploy sifresi ($kullanici)" -AsSecureString
    $sifre = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($gizli))
}
if ([string]::IsNullOrWhiteSpace($sifre)) { throw "Sifre girilmedi." }

# ---------------------------------------------------------------- 4) publish
Basamak "Yayin paketi hazirlaniyor"
$proje = Join-Path $PSScriptRoot 'Kesko.Portal\Kesko.Portal.csproj'
if (Test-Path $CikisKlasoru) { Remove-Item $CikisKlasoru -Recurse -Force }

& dotnet publish $proje -c Release -o $CikisKlasoru --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish basarisiz oldu." }

# Kurumsal sitenin gercekten kopyalandigini dogrula: eksik paket gitmesin.
foreach ($gerekli in 'wwwroot\index.html', 'wwwroot\assets\css\style.css', 'web.config') {
    if (-not (Test-Path (Join-Path $CikisKlasoru $gerekli))) {
        throw "Yayin paketi eksik: $gerekli bulunamadi."
    }
}
Write-Host "  Paket tamam: $CikisKlasoru"

# ---------------------------------------------------------------- 5) sync
# App_Data ve logs KORUNUR. Bu -skip kurallari olmadan sunucudaki
# veritabani ve sifreleme anahtarlari silinir.
$korunan = @('App_Data', 'logs')

$arg = @(
    '-verb:sync'
    "-source:contentPath=`"$CikisKlasoru`""
    "-dest:contentPath=`"$site`",computerName=`"$bilgisayar`",userName=`"$kullanici`",password=`"$sifre`",authtype=`"Basic`",includeAcls=`"False`""
    '-allowUntrusted'
    '-enableRule:AppOffline'
    '-retryAttempts:3'
)
foreach ($k in $korunan) {
    $arg += "-skip:objectName=dirPath,absolutePath=`"$k`""
    $arg += "-skip:objectName=filePath,absolutePath=`"$k`""
}
if ($Prova) { $arg += '-whatif' }

Basamak $(if ($Prova) { "PROVA - sunucuda hicbir sey degismeyecek" } else { "Sunucuya gonderiliyor" })
Write-Host "  Korunan   : $($korunan -join ', ')"

& $msdeploy @arg
if ($LASTEXITCODE -ne 0) { throw "Yayin basarisiz oldu (msdeploy cikis kodu $LASTEXITCODE)." }

if ($Prova) {
    Basamak "Prova bitti. Gercekten yayinlamak icin -Prova olmadan calistirin."
    return
}

# ---------------------------------------------------------------- 6) kontrol
Basamak "Canli kontrol"
Start-Sleep -Seconds 20

$kok = $p.destinationAppUrl -replace '^http:', 'https:'
foreach ($yol in '/', '/admin/giris', '/api/site-ayar') {
    try {
        $y = Invoke-WebRequest -Uri "$kok$yol" -UseBasicParsing -TimeoutSec 30
        Write-Host ("  {0,-18} {1}" -f $yol, $y.StatusCode) -ForegroundColor Green
    } catch {
        Write-Host ("  {0,-18} HATA: {1}" -f $yol, $_.Exception.Message) -ForegroundColor Red
    }
}

Basamak "Bitti: $kok"
