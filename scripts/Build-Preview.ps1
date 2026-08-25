param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)

$ErrorActionPreference = "Stop"
$project = Join-Path $Root "GlyphEcho.csproj"
$updaterProject = Join-Path $Root "GlyphEcho.Updater\GlyphEcho.Updater.csproj"
$version = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.Version
$previewRoot = Join-Path $Root "temp\preview"

New-Item -ItemType Directory -Force $previewRoot | Out-Null
& (Join-Path $Root "scripts\Generate-AppIcon.ps1") -Root $Root | Out-Null
$iconRevision = (Get-FileHash -LiteralPath (Join-Path $Root "Assets\GlyphEcho.ico") -Algorithm SHA256).Hash.Substring(0, 8).ToLowerInvariant()
$output = Join-Path $previewRoot "GlyphEcho-$version-Lite-local-$iconRevision"
$updaterOutput = Join-Path $previewRoot "updater-lite-$iconRevision"
$archive = Join-Path $previewRoot "GlyphEcho-$version-Lite-local-$iconRevision.zip"
foreach ($target in @($output, $updaterOutput)) {
    $fullTarget = [IO.Path]::GetFullPath($target)
    $fullPreviewRoot = [IO.Path]::GetFullPath($previewRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$fullTarget.StartsWith($fullPreviewRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Preview target escaped temp directory: $fullTarget" }
    if (Test-Path -LiteralPath $fullTarget) { Remove-Item -LiteralPath $fullTarget -Recurse -Force }
}
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }

dotnet publish $project -c Release -r win-x64 --self-contained false -o $output
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho Lite publish failed." }
dotnet publish $updaterProject -c Release -r win-x64 --self-contained false -p:Version=$version -o $updaterOutput
if ($LASTEXITCODE -ne 0) { throw "GlyphEcho updater publish failed." }
Copy-Item -LiteralPath (Join-Path $updaterOutput "GlyphEcho.Updater.exe") -Destination $output -Force
[IO.File]::WriteAllText((Join-Path $output ".glyph-echo-channel"), "lite", [Text.UTF8Encoding]::new($false))
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $archive -Force

$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[pscustomobject]@{
    Kind = "Local preview, not a Release"
    Version = $version
    IconRevision = $iconRevision
    Directory = $output
    Archive = $archive
    Size = (Get-Item -LiteralPath $archive).Length
    SHA256 = $hash
}
