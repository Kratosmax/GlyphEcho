param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")))

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$assetRoot = Join-Path $Root "Assets"
New-Item -ItemType Directory -Force $assetRoot | Out-Null
$pngPath = Join-Path $assetRoot "GlyphEcho.png"
$icoPath = Join-Path $assetRoot "GlyphEcho.ico"
$generationRoot = Join-Path $Root "temp\icon-generation"
$generatedPngPath = Join-Path $generationRoot "GlyphEcho.png"
New-Item -ItemType Directory -Force $generationRoot | Out-Null

function Test-BitmapPixelsEqual([string]$LeftPath, [string]$RightPath) {
    if (!(Test-Path -LiteralPath $LeftPath) -or !(Test-Path -LiteralPath $RightPath)) { return $false }
    $left = [Drawing.Bitmap]::new($LeftPath)
    $right = [Drawing.Bitmap]::new($RightPath)
    try {
        if ($left.Width -ne $right.Width -or $left.Height -ne $right.Height) { return $false }
        for ($y = 0; $y -lt $left.Height; $y++) {
            for ($x = 0; $x -lt $left.Width; $x++) {
                if ($left.GetPixel($x, $y).ToArgb() -ne $right.GetPixel($x, $y).ToArgb()) { return $false }
            }
        }
        return $true
    } finally {
        $left.Dispose()
        $right.Dispose()
    }
}

$bitmap = [Drawing.Bitmap]::new(256, 256, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $tail = [Drawing.PointF[]]@(
        [Drawing.PointF]::new(73, 183),
        [Drawing.PointF]::new(53, 229),
        [Drawing.PointF]::new(116, 192)
    )
    $shellBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 20, 125, 118))
    $graphics.FillPolygon($shellBrush, $tail)
    $shell = [Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $shell.AddArc(25, 30, 54, 54, 180, 90)
        $shell.AddArc(177, 30, 54, 54, 270, 90)
        $shell.AddArc(177, 157, 54, 54, 0, 90)
        $shell.AddArc(25, 157, 54, 54, 90, 90)
        $shell.CloseFigure()
        $graphics.FillPath($shellBrush, $shell)
    } finally { $shell.Dispose() }
    $shellBrush.Dispose()

    $faceBrush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    try {
        $graphics.FillEllipse($faceBrush, 77, 91, 25, 31)
        $graphics.FillEllipse($faceBrush, 151, 91, 25, 31)
    } finally { $faceBrush.Dispose() }
    $smile = [Drawing.Pen]::new([Drawing.Color]::White, 12)
    try {
        $smile.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $smile.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawArc($smile, 85, 109, 83, 63, 20, 140)
    } finally { $smile.Dispose() }

    $sparkleBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 232, 117, 105))
    try {
        $sparkle = [Drawing.PointF[]]@(
            [Drawing.PointF]::new(220, 14),
            [Drawing.PointF]::new(227, 29),
            [Drawing.PointF]::new(242, 36),
            [Drawing.PointF]::new(227, 43),
            [Drawing.PointF]::new(220, 58),
            [Drawing.PointF]::new(213, 43),
            [Drawing.PointF]::new(198, 36),
            [Drawing.PointF]::new(213, 29)
        )
        $graphics.FillPolygon($sparkleBrush, $sparkle)
    } finally { $sparkleBrush.Dispose() }
    $bitmap.Save($generatedPngPath, [Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

if (!(Test-BitmapPixelsEqual $pngPath $generatedPngPath)) {
    Copy-Item -LiteralPath $generatedPngPath -Destination $pngPath -Force
}
Remove-Item -LiteralPath $generatedPngPath -Force

$source = [Drawing.Image]::FromFile($pngPath)
$entries = [Collections.Generic.List[object]]::new()
try {
    foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
        $scaled = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $scaledGraphics = [Drawing.Graphics]::FromImage($scaled)
        try {
            $scaledGraphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
            $scaledGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $scaledGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scaledGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $scaledGraphics.Clear([Drawing.Color]::Transparent)
            $scaledGraphics.DrawImage($source, 0, 0, $size, $size)
        } finally { $scaledGraphics.Dispose() }

        $memory = [IO.MemoryStream]::new()
        $dibWriter = [IO.BinaryWriter]::new($memory)
        try {
            $xorSize = $size * $size * 4
            $maskStride = [Math]::Ceiling($size / 32.0) * 4
            $maskSize = $maskStride * $size
            $dibWriter.Write([uint32]40)
            $dibWriter.Write([int32]$size)
            $dibWriter.Write([int32]($size * 2))
            $dibWriter.Write([uint16]1)
            $dibWriter.Write([uint16]32)
            $dibWriter.Write([uint32]0)
            $dibWriter.Write([uint32]$xorSize)
            $dibWriter.Write([int32]0); $dibWriter.Write([int32]0)
            $dibWriter.Write([uint32]0); $dibWriter.Write([uint32]0)
            for ($y = $size - 1; $y -ge 0; $y--) {
                for ($x = 0; $x -lt $size; $x++) {
                    $pixel = $scaled.GetPixel($x, $y)
                    $dibWriter.Write([byte]$pixel.B)
                    $dibWriter.Write([byte]$pixel.G)
                    $dibWriter.Write([byte]$pixel.R)
                    $dibWriter.Write([byte]$pixel.A)
                }
            }
            $dibWriter.Write([byte[]]::new($maskSize))
            $dibWriter.Flush()
            $entries.Add([pscustomobject]@{ Size = $size; Bytes = $memory.ToArray() })
        } finally {
            $dibWriter.Dispose()
            $memory.Dispose()
            $scaled.Dispose()
        }
    }
} finally { $source.Dispose() }

$stream = [IO.File]::Create($icoPath)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$entries.Count)
    $offset = 6 + 16 * $entries.Count
    foreach ($entry in $entries) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$entry.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $entries) { $writer.Write([byte[]]$entry.Bytes) }
} finally { $writer.Dispose() }

Get-Item $pngPath, $icoPath | Select-Object FullName, Length, @{ Name = "IconFrames"; Expression = { if ($_.Extension -eq ".ico") { $entries.Count } else { $null } } }
