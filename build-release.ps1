param(
    [string]$DownloadUrlBase = "http://localhost:8000",
    [string]$ReleaseNotes = "Correctifs et améliorations.",
    [string]$GitHubRepo = "",
    [string]$Version = "",
    [string]$SignPfx = "",
    [string]$SignPfxPassword = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$ManifestOnly
)

# ─────────────────────────────────────────────────────────────────────────────
#  build-release.ps1 — Construit la distribution Windows "Mohasabi_setup.exe"
#
#  1. Build du frontend (React) vers src/Factur.Api/wwwroot
#  2. Publication de l'API en autonome (self-contained) win-x64
#  3. Publication du launcher (Mohasabi.exe, fenêtre WebView2) autonome single-file
#  4. Vérification/téléchargement du runtime Microsoft Edge WebView2
#  5. Compilation de l'installateur Inno Setup (WebView2 Runtime embarqué)
#  6. Génération de la source de mise à jour (version.json + setup)
# ─────────────────────────────────────────────────────────────────────────────
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$version = $Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = (Select-String -Path "$root\Directory.Build.props" -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
}
$setupExeName = "Mohasabi_setup.exe"
$staging = "$root\release\staging"
$distDir = "$root\dist\release"
$updateSource = "$root\release\update-source"
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"

# Génère release\update-source\version.json (downloadUrl + empreinte SHA-256)
function New-UpdateManifest {
    param([string]$Version)
    $setupPath = "$distDir\$setupExeName"
    if (-not (Test-Path $setupPath)) { throw "Installateur introuvable : $setupPath" }
    $sha256 = (Get-FileHash -Algorithm SHA256 -Path $setupPath).Hash
    $downloadUrl = if ([string]::IsNullOrWhiteSpace($GitHubRepo)) {
        "$DownloadUrlBase/$setupExeName"
    } else {
        "https://github.com/$GitHubRepo/releases/latest/download/$setupExeName"
    }
    $notes = $ReleaseNotes
    $notesFile = "$root\RELEASE_NOTES.md"
    if (Test-Path $notesFile) {
        try { $notes = [System.IO.File]::ReadAllText($notesFile, [System.Text.Encoding]::UTF8) } catch { }
    }
    $manifest = @{
        version      = $Version
        downloadUrl  = $downloadUrl
        sha256       = $sha256
        releaseNotes = $notes
    }
    $json = $manifest | ConvertTo-Json
    [System.IO.File]::WriteAllText("$updateSource\version.json", $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  Manifest : $updateSource\version.json"
    Write-Host "  SHA-256  : $sha256"
}

# Mode manifest uniquement : régénère la source de mise à jour à partir de
# l'installateur déjà construit (aucune recompilation, aucun build).
if ($ManifestOnly) {
    Write-Host "=== Mode manifest uniquement (installateur existant) ===" -ForegroundColor Cyan
    if (Test-Path $updateSource) { Remove-Item -Recurse -Force $updateSource }
    New-Item -ItemType Directory -Path $updateSource -Force | Out-Null
    Copy-Item "$distDir\$setupExeName" "$updateSource\$setupExeName"
    Copy-Item "$root\Mohasabi_README.txt" "$updateSource\Mohasabi_README.txt"
    New-UpdateManifest -Version $version
    Write-Host "`n=== Terminé (manifest uniquement) ===" -ForegroundColor Green
    Write-Host "Fichiers à publier comme assets du Release GitHub :"
    Write-Host "  - $updateSource\$setupExeName"
    Write-Host "  - $updateSource\Mohasabi_README.txt"
    Write-Host "  - $updateSource\version.json"
    exit 0
}

# Runtime WebView2 Evergreen Standalone Installer (x64), mis en cache localement.
$webView2Exe = "$root\.cache\webview2\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
$webView2Url = "https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/f3274495-ff02-440e-b522-2d4129a911e8/MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
$webView2Sha256 = "04B9F08D839C8C06F34A85ACEA0D9F1568D3D8AA309A77619AAA46BB29ADE0F8"

Write-Host "=== Version : $version ===" -ForegroundColor Cyan

# 1) Frontend
Write-Host "`n[1/6] Build frontend..."
Push-Location "$root\frontend"
$oldEap = $ErrorActionPreference
try {
    # EAP relâché : node/vite écrivent des messages sur stderr qui, avec
    # ErrorActionPreference=Stop, feraient échouer la commande en PowerShell 5.1.
    $ErrorActionPreference = "Continue"
    try {
        if (Test-Path "node_modules") { npm install --no-audit --no-fund 2>&1 | Out-Null }
        else { npm ci --no-audit --no-fund 2>&1 | Out-Null }
        npm run build 2>&1
        $npmCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldEap
    }
}
finally {
    Pop-Location
}
if ($npmCode -ne 0) { throw "Échec du build frontend." }

# 2) Publication API self-contained
Write-Host "`n[2/6] Publication API (self-contained win-x64)..."
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
dotnet publish "$root\src\Factur.Api\Factur.Api.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:DebugSymbols=false -p:DebugType=None `
    -p:Version=$version `
    -o "$staging\app" --nologo
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication de l'API." }

# 3) Publication launcher (single-file self-contained, WebView2 intégré)
Write-Host "`n[3/6] Publication launcher..."
$launcherOut = "$staging\launcher-publish"
dotnet publish "$root\tools\Mohasabi.Launcher\Mohasabi.Launcher.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$version `
    -o $launcherOut --nologo
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication du launcher." }
Copy-Item "$launcherOut\Mohasabi.exe" "$staging\Mohasabi.exe"
Remove-Item -Recurse -Force $launcherOut

Copy-Item "$root\assets\mohasabi.ico" "$staging\mohasabi.ico"
Copy-Item "$root\assets\mohasabi.png" "$staging\mohasabi.png"
Copy-Item "$root\installer\launcher.json" "$staging\launcher.json"

# 4) Runtime WebView2 (téléchargé une seule fois, puis vérifié)
Write-Host "`n[4/6] Runtime Microsoft Edge WebView2..."
if (Test-Path $webView2Exe) {
    $h = (Get-FileHash -Algorithm SHA256 -Path $webView2Exe).Hash
    if ($h -ne $webView2Sha256) {
        Write-Host "  Empreinte inattendue ($h), re-téléchargement..." -ForegroundColor Yellow
        Remove-Item -Force $webView2Exe
    }
}
if (-not (Test-Path $webView2Exe)) {
    Write-Host "  Téléchargement du runtime WebView2 (≈200 Mo, une seule fois)..."
    New-Item -ItemType Directory -Force -Path (Split-Path $webView2Exe) | Out-Null
    Invoke-WebRequest -Uri $webView2Url -OutFile $webView2Exe -UseBasicParsing
    $h = (Get-FileHash -Algorithm SHA256 -Path $webView2Exe).Hash
    if ($h -ne $webView2Sha256) { throw "Empreinte SHA-256 du runtime WebView2 inattendue : $h" }
}
Write-Host "  Runtime WebView2 prêt : $webView2Exe"

# 5) Installateur Inno Setup
Write-Host "`n[5/6] Compilation de l'installateur..."
if (-not (Test-Path $iscc)) { throw "ISCC introuvable : $iscc" }
& $iscc "$root\installer\installer.iss" /DSourceStaging="$staging" /DVersion=$version "/DWebView2Installer=$webView2Exe"
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation Inno Setup." }

# 5b) Signature code (optionnelle) de l'installateur, puis nouvelle empreinte SHA-256
$setupPath = "$distDir\$setupExeName"
if (-not [string]::IsNullOrWhiteSpace($SignPfx)) {
    Write-Host "`n[5b/6] Signature de l'installateur..."
    if (-not (Test-Path $SignPfx)) { throw "Certificat introuvable : $SignPfx" }
    $signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "x64|x86|amd64" } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { $signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 }
    if (-not $signtool) { throw "signtool.exe introuvable dans Windows Kits." }
    $signArgs = @("sign", "/fd", "SHA256", "/f", $SignPfx)
    if (-not [string]::IsNullOrWhiteSpace($SignPfxPassword)) { $signArgs += @("/p", $SignPfxPassword) }
    $signArgs += @("/tr", $TimestampUrl, "/td", "SHA256")
    $signArgs += @("`"$setupPath`"")
    & $signtool.FullName @signArgs
    if ($LASTEXITCODE -ne 0) { throw "Échec de la signature : signtool code $LASTEXITCODE" }
    Write-Host "  Installateur signé : $setupPath"
}

# 6) Source de mise à jour
Write-Host "`n[6/6] Source de mise à jour..."
if (Test-Path $updateSource) { Remove-Item -Recurse -Force $updateSource }
New-Item -ItemType Directory -Path $updateSource -Force | Out-Null
Copy-Item "$distDir\$setupExeName" "$updateSource\$setupExeName"
Copy-Item "$root\Mohasabi_README.txt" "$updateSource\Mohasabi_README.txt"
New-UpdateManifest -Version $version

# Documentation livrée à côté de l'installateur
Copy-Item "$root\Mohasabi_README.txt" "$distDir\Mohasabi_README.txt"

Write-Host "`n=== Terminé ===" -ForegroundColor Green
Write-Host "Installateur  : $distDir\$setupExeName"
Write-Host "Documentation : $distDir\Mohasabi_README.txt"
Write-Host "Source MAJ    : $updateSource"
Write-Host ""
if ([string]::IsNullOrWhiteSpace($GitHubRepo)) {
    Write-Host "Pour activer la vérification de mises à jour, pointer launcher.json (dans le dossier"
    Write-Host "d'installation) vers le manifest, ex. :  http://localhost:8000/version.json"
} else {
    Write-Host "Manifest cible : https://github.com/$GitHubRepo/releases/latest/download/version.json"
    Write-Host "Publier comme assets du Release GitHub : $setupExeName, Mohasabi_README.txt, version.json"
}
