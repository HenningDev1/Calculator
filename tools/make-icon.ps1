# Draws the Calculator app icon and writes Calculator.ico (multi-size, PNG entries) + Assets\icon-256.png.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force (Join-Path $root 'Assets') | Out-Null

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $s = [single]$size

    # Rounded "squircle" tile with a soft dark gradient.
    $m = $s * 0.04
    $tile = New-RoundRect $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.23)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(0, $s)), ([System.Drawing.Color]::FromArgb(255, 58, 58, 62)), ([System.Drawing.Color]::FromArgb(255, 18, 18, 20))
    $g.FillPath($grad, $tile)

    # 2x2 round keys: + − × in grey, = in orange.
    $keyR = $s * 0.17
    $centers = @(@(0.33, 0.33), @(0.67, 0.33), @(0.33, 0.67), @(0.67, 0.67))
    $grey = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 80, 80, 84))
    $orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 159, 10))
    $white = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.2, $s * 0.035))
    $white.StartCap = 'Round'; $white.EndCap = 'Round'
    for ($i = 0; $i -lt 4; $i++) {
        $cx = $s * $centers[$i][0]; $cy = $s * $centers[$i][1]
        $brush = if ($i -eq 3) { $orange } else { $grey }
        $g.FillEllipse($brush, $cx - $keyR, $cy - $keyR, 2 * $keyR, 2 * $keyR)
        $a = $keyR * 0.45
        switch ($i) {
            0 { $g.DrawLine($white, $cx - $a, $cy, $cx + $a, $cy); $g.DrawLine($white, $cx, $cy - $a, $cx, $cy + $a) }
            1 { $g.DrawLine($white, $cx - $a, $cy, $cx + $a, $cy) }
            2 { $b = $a * 0.8; $g.DrawLine($white, $cx - $b, $cy - $b, $cx + $b, $cy + $b); $g.DrawLine($white, $cx - $b, $cy + $b, $cx + $b, $cy - $b) }
            3 { $o = $a * 0.38; $g.DrawLine($white, $cx - $a, $cy - $o, $cx + $a, $cy - $o); $g.DrawLine($white, $cx - $a, $cy + $o, $cx + $a, $cy + $o) }
        }
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($sz in $sizes) {
    $bmp = Draw-Icon $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($sz -eq 256) { $bmp.Save((Join-Path $root 'Assets\icon-256.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    $pngs += , $ms.ToArray()
}

# ICO container with PNG-compressed entries.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $len = $pngs[$i].Length
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $root 'Calculator.ico'), $out.ToArray())
Write-Output "Wrote Calculator.ico"
