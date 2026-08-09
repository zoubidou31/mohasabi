param(
    [string]$OutputPath = "$PSScriptRoot\..\installer\mohasabi.ico",
    [string]$Name = "M"
)

Add-Type -AssemblyName System.Drawing

function New-IconPngBytes {
    param([int]$Size)

    $w = $Size - 2
    $h = $Size - 2
    $radius = [int]($Size * 0.22)
    $d = $radius * 2

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = [System.Drawing.Rectangle]::new(1, 1, $w, $h)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $green = [System.Drawing.Color]::FromArgb(255, 21, 115, 71)
    $brush = New-Object System.Drawing.SolidBrush($green)
    $g.FillPath($brush, $path)

    $fontSize = [float]($Size * 0.52)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $textRect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
    $g.DrawString($Name, $font, $white, $textRect, $fmt)

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()

    $ms.Dispose()
    $font.Dispose()
    $white.Dispose()
    $fmt.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $g.Dispose()
    $bmp.Dispose()

    return ,$bytes
}

$png = New-IconPngBytes -Size 256

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$bw.Write([UInt16]0)                    # réservé
$bw.Write([UInt16]1)                    # type : icône
$bw.Write([UInt16]1)                    # nombre d'images

$bw.Write([Byte]0)                      # largeur (256)
$bw.Write([Byte]0)                      # hauteur (256)
$bw.Write([Byte]0)                      # palette
$bw.Write([Byte]0)                      # réservé
$bw.Write([UInt16]1)                    # plans
$bw.Write([UInt16]32)                   # bpp
$bw.Write([UInt32]$png.Length)          # taille des données PNG
$bw.Write([UInt32]22)                   # offset des données

$bw.Write($png)
$bw.Flush()

$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($OutputPath, $ms.ToArray())

$bw.Dispose()
$ms.Dispose()

Write-Output "Icône générée : $OutputPath ($((Get-Item $OutputPath).Length) octets)"
