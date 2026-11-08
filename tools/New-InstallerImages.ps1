<#
.SYNOPSIS
  Erzeugt die Grafiken für die MSI-Oberfläche (WiX): Dialogbild 493 × 312 und Banner 493 × 58.

.DESCRIPTION
  Links im Dialogbild ein blauer Streifen mit dem App-Icon, rechts weiß (dort steht der Text).
  Aufruf aus dem Repo-Stamm, nachdem tools/New-AppIcon.ps1 die Icons erzeugt hat:

    pwsh tools/New-InstallerImages.ps1
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\installer\Resources')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$iconPath = Join-Path $PSScriptRoot '..\src\LSMDisplayResizer\Resources\app.ico'
$dark = [System.Drawing.Color]::FromArgb(255, 14, 52, 104)
$light = [System.Drawing.Color]::FromArgb(255, 42, 110, 190)

function Get-IconBitmap([int]$size) {
    $icon = [System.Drawing.Icon]::new($iconPath, $size, $size)
    try { return $icon.ToBitmap() } finally { $icon.Dispose() }
}

function New-Canvas([int]$w, [int]$h) {
    $bmp = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::White)
    return $bmp, $g
}

New-Item -ItemType Directory -Force $OutDir | Out-Null

# Dialogbild (Willkommen/Abschluss): linker Streifen 164 px
$bmp, $g = New-Canvas 493 312
$panel = [System.Drawing.Rectangle]::new(0, 0, 164, 312)
$brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($panel, $light, $dark, 90.0)
$g.FillRectangle($brush, $panel)
$icon = Get-IconBitmap 64
$g.DrawImage($icon, 50, 70, 64, 64)
$font = [System.Drawing.Font]::new('Segoe UI Semibold', 13, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$format = [System.Drawing.StringFormat]::new(); $format.Alignment = 'Center'
$g.DrawString("LSM`nDisplay Resizer", $font, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(0, 150, 164, 60), $format)
$small = [System.Drawing.Font]::new('Segoe UI', 10, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString("kostenlos · MIT-Lizenz", $small, [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(210, 255, 255, 255)), [System.Drawing.RectangleF]::new(0, 280, 164, 20), $format)
$g.Dispose()
$bmp.Save((Join-Path $OutDir 'Dialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$bmp.Dispose()

# Banner (obere Leiste der Folgeseiten): Icon rechts
$bmp, $g = New-Canvas 493 58
$g.FillRectangle([System.Drawing.SolidBrush]::new($dark), 0, 56, 493, 2)
$icon = Get-IconBitmap 48
$g.DrawImage($icon, 433, 5, 48, 48)
$g.Dispose()
$bmp.Save((Join-Path $OutDir 'Banner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$bmp.Dispose()

Write-Host "Geschrieben: $OutDir\Dialog.bmp, Banner.bmp"
