#Requires -Version 5.1
<#
.SYNOPSIS
    Regenerates WallpaperSwitcher.Desktop/Assets/AppIcon.ico from AppIcon.png.

.DESCRIPTION
    The .ico is committed to the repository, so this only needs running when the
    icon artwork changes. Keeping it a committed asset means contributors do not
    need an image toolchain to build.

    Sizes up to 128px are written as uncompressed BMP/DIB entries and 256px as
    PNG. That is the layout standard icon tooling produces. PNG entries at small
    sizes are legal since Vista but GDI+ cannot decode them, so anything drawing
    the icon through System.Drawing - including some installers and shell
    surfaces - would fail to render it.
#>
[CmdletBinding()]
param(
    [string]$SourcePng = (Join-Path $PSScriptRoot '..\WallpaperSwitcher.Desktop\Assets\AppIcon.png'),
    [string]$OutputIco = (Join-Path $PSScriptRoot '..\WallpaperSwitcher.Desktop\Assets\AppIcon.ico')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = @(256, 128, 96, 64, 48, 40, 32, 24, 20, 16)

function ConvertTo-IcoDib {
    <#
        Builds an ICO "BMP" entry: a BITMAPINFOHEADER whose height is doubled to
        cover the colour data plus the AND mask, then bottom-up BGRA pixels, then
        the mask itself. The mask is all zeroes because the alpha channel already
        carries transparency, but it must still be present and 32-bit aligned.
    #>
    param([System.Drawing.Bitmap]$Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $maskStride = [int](([math]::Floor(($width + 31) / 32)) * 4)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    $writer.Write([uint32]40)               # biSize
    $writer.Write([int32]$width)            # biWidth
    $writer.Write([int32]($height * 2))     # biHeight (colour + mask)
    $writer.Write([uint16]1)                # biPlanes
    $writer.Write([uint16]32)               # biBitCount
    $writer.Write([uint32]0)                # biCompression (BI_RGB)
    $writer.Write([uint32](($width * $height * 4) + ($maskStride * $height)))
    $writer.Write([int32]0)                 # biXPelsPerMeter
    $writer.Write([int32]0)                 # biYPelsPerMeter
    $writer.Write([uint32]0)                # biClrUsed
    $writer.Write([uint32]0)                # biClrImportant

    $data = $Bitmap.LockBits(
        (New-Object System.Drawing.Rectangle(0, 0, $width, $height)),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $row = New-Object byte[] ($width * 4)
        for ($y = $height - 1; $y -ge 0; $y--) {
            $scan = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
            [System.Runtime.InteropServices.Marshal]::Copy($scan, $row, 0, $row.Length)
            $writer.Write($row)
        }
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    $writer.Write((New-Object byte[] ($maskStride * $height)))
    $writer.Flush()

    $bytes = $stream.ToArray()
    $stream.Dispose()

    # The leading comma stops PowerShell unrolling the array on return, which
    # would hand the caller an Object[] and bind BinaryWriter's single-byte
    # overload instead of the byte[] one.
    return ,$bytes
}

$source = [System.Drawing.Image]::FromFile((Resolve-Path $SourcePng))
try {
    $encoded = foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage($source, 0, 0, $size, $size)
        }
        finally {
            $graphics.Dispose()
        }

        if ($size -ge 256) {
            $stream = New-Object System.IO.MemoryStream
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $bytes = $stream.ToArray()
            $stream.Dispose()
        }
        else {
            $bytes = ConvertTo-IcoDib -Bitmap $bitmap
        }

        $bitmap.Dispose()
        [pscustomobject]@{ Size = $size; Bytes = $bytes }
    }
}
finally {
    $source.Dispose()
}

$output = [System.IO.File]::Create((Join-Path (Split-Path $OutputIco -Parent) (Split-Path $OutputIco -Leaf)))
try {
    $writer = New-Object System.IO.BinaryWriter($output)

    # ICONDIR: reserved, type (1 = icon), image count.
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$encoded.Count)

    # Image data starts after the directory and one 16-byte entry per image.
    $offset = 6 + (16 * $encoded.Count)

    foreach ($image in $encoded) {
        # A dimension of 0 means 256 in the ICO format.
        $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)      # width
        $writer.Write([byte]$dimension)      # height
        $writer.Write([byte]0)               # palette size (0 = truecolour)
        $writer.Write([byte]0)               # reserved
        $writer.Write([uint16]1)             # colour planes
        $writer.Write([uint16]32)            # bits per pixel
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }

    foreach ($image in $encoded) {
        $writer.Write($image.Bytes)
    }

    $writer.Flush()
}
finally {
    $output.Dispose()
}

$result = Get-Item $OutputIco
Write-Output "Wrote $($result.FullName) ($([math]::Round($result.Length / 1KB, 1)) KB, $($encoded.Count) sizes)"
