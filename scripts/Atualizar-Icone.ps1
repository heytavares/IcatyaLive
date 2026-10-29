[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assets = Join-Path $projectRoot 'src\Icatya\Assets'
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Bitmap]::FromFile((Join-Path $assets 'logo-icatya.png'))
try {
    $left = $source.Width; $top = $source.Height; $right = -1; $bottom = -1
    for ($y = 0; $y -lt $source.Height; $y++) {
        for ($x = 0; $x -lt $source.Width; $x++) {
            if ($source.GetPixel($x, $y).A -gt 0) {
                $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
                $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    if ($right -lt $left) { throw 'A imagem do icone esta vazia.' }
    $crop = [Drawing.Rectangle]::new($left, $top, $right-$left+1, $bottom-$top+1)
    # PNG-backed ICO frames retain the original transparency at every Windows icon size.
    $frames = foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
        $bitmap = New-Object Drawing.Bitmap($size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = New-Object IO.MemoryStream
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $padding = [Math]::Max(1, $size / 32)
            $scale = [Math]::Min(($size - 2*$padding) / $crop.Width, ($size - 2*$padding) / $crop.Height)
            $width = [int][Math]::Round($crop.Width * $scale)
            $height = [int][Math]::Round($crop.Height * $scale)
            $destination = [Drawing.Rectangle]::new([int](($size-$width)/2), [int](($size-$height)/2), $width, $height)
            $graphics.DrawImage($source, $destination, $crop, [Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            [PSCustomObject]@{ Size = $size; Bytes = $stream.ToArray() }
        }
        finally { $graphics.Dispose(); $bitmap.Dispose(); $stream.Dispose() }
    }
    [IO.File]::WriteAllBytes((Join-Path $assets 'BrandMark.png'), ($frames | Where-Object Size -eq 64).Bytes)
    $output = [IO.File]::Create((Join-Path $assets 'Icatya.ico'))
    $writer = New-Object IO.BinaryWriter($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    }
    finally { $writer.Dispose(); $output.Dispose() }
    Write-Host 'Icone do Icatya atualizado em sete tamanhos, de 16 a 256 pixels.'
}
finally { $source.Dispose() }
