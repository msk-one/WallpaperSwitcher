<#
.SYNOPSIS
    Renders docs/banner.png, the image at the top of the README.

.DESCRIPTION
    The banner is a day-to-night sky with the sun, moon and hills from the
    application icon. Generated rather than hand-drawn so it can be re-rendered
    when the wording or the size changes. Windows only: it uses System.Drawing
    and the Segoe UI font.
#>
param([string]$Out = 'docs\banner.png')

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$W = 1280; $H = 340
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'ClearTypeGridFit'
$g.InterpolationMode = 'HighQualityBicubic'

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function A([System.Drawing.Color]$c, [int]$a) { [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }

# Day on the left, night on the right.
$rect = New-Object System.Drawing.Rectangle 0, 0, $W, $H
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, (C '#7FC4F2'), (C '#070B18'), 0.0)
$blend = New-Object System.Drawing.Drawing2D.ColorBlend 4
$blend.Colors = @((C '#8ACDF5'), (C '#4E86C8'), (C '#1B2B4D'), (C '#070B18'))
$blend.Positions = @(0.0, 0.38, 0.72, 1.0)
$grad.InterpolationColors = $blend
$g.FillRectangle($grad, $rect)
$grad.Dispose()

function Glow([int]$cx, [int]$cy, [int]$r, [System.Drawing.Color]$col, [int]$alpha) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse(($cx - $r), ($cy - $r), ($r * 2), ($r * 2))
    $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $pgb.CenterColor = (A $col $alpha)
    $pgb.SurroundColors = @((A $col 0))
    $g.FillPath($pgb, $path)
    $pgb.Dispose(); $path.Dispose()
}

# Sun, low on the day side.
Glow 128 118 130 (C '#FFF2C0') 150
$sun = New-Object System.Drawing.SolidBrush (A (C '#FFF6DA') 245)
$g.FillEllipse($sun, 90, 80, 76, 76)
$sun.Dispose()

# Stars, then the moon, on the night side.
$rand = New-Object System.Random 7
$star = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
for ($i = 0; $i -lt 70; $i++) {
    $x = $rand.Next(760, $W - 10)
    $y = $rand.Next(10, 210)
    # Fade the field out towards the day side so it does not look pasted on.
    $strength = [Math]::Min(1.0, ($x - 760) / 380.0)
    $alpha = [int](($rand.Next(70, 230)) * $strength)
    if ($alpha -lt 20) { continue }
    $size = if ($rand.Next(0, 10) -gt 7) { 3 } else { 2 }
    $star.Color = [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255)
    $g.FillEllipse($star, $x, $y, $size, $size)
}
$star.Dispose()

Glow 1152 112 110 (C '#CFE0FF') 90
$moon = New-Object System.Drawing.SolidBrush (A (C '#EAF1FF') 240)
$g.FillEllipse($moon, 1114, 78, 72, 72)
$moon.Dispose()

# Hills, echoing the application icon. Filled left-to-right so they follow the
# sky from day into night instead of cutting a green band across it.
function Hill([int]$baseY, [int]$amp, [string]$dayHex, [string]$nightHex, [double]$phase) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($x = 0; $x -le $W; $x += 8) {
        $y = $baseY + [Math]::Sin(($x / 210.0) + $phase) * $amp
        $points.Add((New-Object System.Drawing.PointF $x, $y))
    }
    $points.Add((New-Object System.Drawing.PointF $W, $H))
    $points.Add((New-Object System.Drawing.PointF 0, $H))
    $path.AddPolygon($points.ToArray())
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle 0, 0, $W, $H), (C $dayHex), (C $nightHex), 0.0)
    $g.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()
}
Hill 270 14 '#5A9A57' '#132436' 1.2
Hill 296 18 '#3D7A45' '#0A141F' 2.6

$center = New-Object System.Drawing.StringFormat
$center.Alignment = 'Center'
$center.LineAlignment = 'Center'

function Text([string]$s, [string]$family, [single]$size, [System.Drawing.FontStyle]$style, [int]$y, [System.Drawing.Color]$col, [int]$alpha, [int]$shadow) {
    $font = New-Object System.Drawing.Font $family, $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
    # The layout rectangle clips, so it has to be tall enough for the glyphs and
    # centred on $y rather than starting there.
    $boxHeight = $size * 2.4
    $box = New-Object System.Drawing.RectangleF 0, ($y - $boxHeight / 2), $W, $boxHeight
    if ($shadow -gt 0) {
        $sb = New-Object System.Drawing.SolidBrush (A ([System.Drawing.Color]::Black) $shadow)
        $shifted = New-Object System.Drawing.RectangleF 2, ($y - $boxHeight / 2 + 3), $W, $boxHeight
        $g.DrawString($s, $font, $sb, $shifted, $center)
        $sb.Dispose()
    }
    $brush = New-Object System.Drawing.SolidBrush (A $col $alpha)
    $g.DrawString($s, $font, $brush, $box, $center)
    $brush.Dispose(); $font.Dispose()
}

Text 'Wallpaper Switcher' 'Segoe UI' 74 ([System.Drawing.FontStyle]::Bold) 128 ([System.Drawing.Color]::White) 255 90
Text 'Day and night wallpapers, from a folder you own' 'Segoe UI' 30 ([System.Drawing.FontStyle]::Regular) 200 ([System.Drawing.Color]::White) 225 70
Text 'Windows  ·  macOS  ·  Linux' 'Segoe UI' 22 ([System.Drawing.FontStyle]::Regular) 296 ([System.Drawing.Color]::White) 165 60

$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Wrote $Out"

