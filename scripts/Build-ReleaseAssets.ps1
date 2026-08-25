param(
    [string]$Version = "0.2.0",
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)
$ErrorActionPreference = "Stop"
$project = Join-Path $Root "GlyphEcho.csproj"
$updater = Join-Path $Root "GlyphEcho.Updater\GlyphEcho.Updater.csproj"
$buildRoot = Join-Path $Root "temp\package"
$assetRoot = Join-Path $Root "temp\release-assets"
Remove-Item $buildRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $assetRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $buildRoot,$assetRoot | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained false -o (Join-Path $buildRoot "Lite")
dotnet publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $buildRoot "Full")
dotnet publish $updater -c Release -r win-x64 --self-contained false -o (Join-Path $Root "temp\updater-lite")
dotnet publish $updater -c Release -r win-x64 --self-contained true -o (Join-Path $Root "temp\updater-full")
Copy-Item (Join-Path $Root "temp\updater-lite\GlyphEcho.Updater.exe") (Join-Path $buildRoot "Lite") -Force
Copy-Item (Join-Path $Root "temp\updater-full\GlyphEcho.Updater.exe") (Join-Path $buildRoot "Full") -Force
Compress-Archive -Path (Join-Path $buildRoot "Lite\*") -DestinationPath (Join-Path $assetRoot "GlyphEcho-$Version-Lite.zip") -Force
Compress-Archive -Path (Join-Path $buildRoot "Full\*") -DestinationPath (Join-Path $assetRoot "GlyphEcho-$Version-Full.zip") -Force
if (Get-Command iscc -ErrorAction SilentlyContinue) {
    $iss = Join-Path $Root "installer\GlyphEcho.iss"
    iscc "/DAPP_VERSION=$Version" "/DMODE=Lite" $iss
    iscc "/DAPP_VERSION=$Version" "/DMODE=Full" $iss
}
Get-ChildItem $assetRoot | Select-Object Name,Length
