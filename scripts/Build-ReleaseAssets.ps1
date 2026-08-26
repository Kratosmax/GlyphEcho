param(
    [string]$Version,
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)
$ErrorActionPreference = "Stop"
$project = Join-Path $Root "GlyphEcho.csproj"
$updater = Join-Path $Root "GlyphEcho.Updater\GlyphEcho.Updater.csproj"
$projectVersion = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $projectVersion }
if ($Version -ne $projectVersion) { throw "Requested version $Version does not match project version $projectVersion." }
$buildRoot = Join-Path $Root "temp\package"
$assetRoot = Join-Path $Root "temp\release-assets"
$updaterLiteRoot = Join-Path $Root "temp\updater-lite"
$updaterFullRoot = Join-Path $Root "temp\updater-full"
Remove-Item $buildRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $assetRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $updaterLiteRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $updaterFullRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $buildRoot,$assetRoot | Out-Null
& (Join-Path $Root "scripts\Generate-AppIcon.ps1") -Root $Root | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained false -o (Join-Path $buildRoot "Lite")
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Lite publish failed." }
dotnet publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $buildRoot "Full")
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Full publish failed." }
dotnet publish $updater -c Release -r win-x64 --self-contained false -p:Version=$Version -o $updaterLiteRoot
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Lite updater publish failed." }
dotnet publish $updater -c Release -r win-x64 --self-contained true -p:Version=$Version -o $updaterFullRoot
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Full updater publish failed." }
Copy-Item (Join-Path $updaterLiteRoot "GlyphEcho.Updater.exe") (Join-Path $buildRoot "Lite") -Force
Copy-Item (Join-Path $updaterFullRoot "GlyphEcho.Updater.exe") (Join-Path $buildRoot "Full") -Force
[IO.File]::WriteAllText((Join-Path $buildRoot "Lite\.glyph-echo-channel"), "lite", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $buildRoot "Full\.glyph-echo-channel"), "full", [Text.UTF8Encoding]::new($false))
Compress-Archive -Path (Join-Path $buildRoot "Lite\*") -DestinationPath (Join-Path $assetRoot "GlyphEcho-$Version-Lite.zip") -Force
Compress-Archive -Path (Join-Path $buildRoot "Full\*") -DestinationPath (Join-Path $assetRoot "GlyphEcho-$Version-Full.zip") -Force
$compiler = (Get-Command iscc -ErrorAction SilentlyContinue)?.Source
if (!$compiler) {
    $compiler = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$compiler) { throw "Inno Setup compiler (iscc) is required for a formal release build." }
$iss = Join-Path $Root "installer\GlyphEcho.iss"
& $compiler "/DAPP_VERSION=$Version" "/DMODE=Lite" $iss
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Lite installer build failed." }
& $compiler "/DAPP_VERSION=$Version" "/DMODE=Full" $iss
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Full installer build failed." }
$expectedAssets = @(
    "GlyphEcho-$Version-Lite.zip", "GlyphEcho-$Version-Full.zip",
    "GlyphEcho-$Version-Lite-Setup.exe", "GlyphEcho-$Version-Full-Setup.exe")
foreach ($name in $expectedAssets) {
    $path = Join-Path $assetRoot $name
    if (!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -le 0) { throw "Release asset missing or empty: $name" }
}
Get-ChildItem $assetRoot | Select-Object Name,Length
