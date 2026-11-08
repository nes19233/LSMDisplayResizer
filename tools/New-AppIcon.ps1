<#
.SYNOPSIS
  Erzeugt die Icons der App (app.ico und app-paused.ico) mit mehreren Größen.

.DESCRIPTION
  Zeichnet zwei Monitore mit einem Pfeil dazwischen und schreibt eine ICO-Datei
  mit 32-Bit-Bildern (16 bis 256 px). Größen bis 64 px werden als DIB gespeichert,
  256 px als PNG. Aufruf aus dem Repo-Stamm:

    pwsh tools/New-AppIcon.ps1
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\src\LSMDisplayResizer\Resources')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = [Math]::Max(0.01, $r * 2)
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size, [bool]$paused) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 32.0
    $g.ScaleTransform($s, $s)

    if ($paused) {
        $bigFrame = [System.Drawing.Color]::FromArgb(255, 96, 96, 96)
        $bigScreen = [System.Drawing.Color]::FromArgb(255, 150, 150, 150)
        $smallFrame = [System.Drawing.Color]::FromArgb(255, 110, 110, 110)
        $smallScreen = [System.Drawing.Color]::FromArgb(255, 170, 170, 170)
    } else {
        $bigFrame = [System.Drawing.Color]::FromArgb(255, 22, 74, 145)
        $bigScreen = [System.Drawing.Color]::FromArgb(255, 66, 135, 220)
        $smallFrame = [System.Drawing.Color]::FromArgb(255, 20, 120, 88)
        $smallScreen = [System.Drawing.Color]::FromArgb(255, 74, 196, 150)
    }

    # Großer Monitor (links)
    $p = New-RoundedPath 0.5 3 21 15 2
    $g.FillPath([System.Drawing.SolidBrush]::new($bigFrame), $p)
    $p = New-RoundedPath 2.5 5 17 11 1
    $g.FillPath([System.Drawing.SolidBrush]::new($bigScreen), $p)
    $g.FillRectangle([System.Drawing.SolidBrush]::new($bigFrame), 9, 18, 4, 3)
    $p = New-RoundedPath 5.5 20.5 11 2.5 1
    $g.FillPath([System.Drawing.SolidBrush]::new($bigFrame), $p)

    # Kleiner Monitor (rechts unten)
    $p = New-RoundedPath 18 13 13.5 10 1.5
    $g.FillPath([System.Drawing.SolidBrush]::new($smallFrame), $p)
    $p = New-RoundedPath 19.5 14.5 10.5 7 0.8
    $g.FillPath([System.Drawing.SolidBrush]::new($smallScreen), $p)
    $g.FillRectangle([System.Drawing.SolidBrush]::new($smallFrame), 23.5, 23, 3, 2.5)
    $p = New-RoundedPath 21 25 8.5 2.2 1
    $g.FillPath([System.Drawing.SolidBrush]::new($smallFrame), $p)

    # Pfeil (weiß mit dunkler Kontur) vom großen zum kleinen Monitor
    $arrow = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(5, 8.5),
        [System.Drawing.PointF]::new(13, 8.5),
        [System.Drawing.PointF]::new(13, 5.5),
        [System.Drawing.PointF]::new(18.5, 10.5),
        [System.Drawing.PointF]::new(13, 15.5),
        [System.Drawing.PointF]::new(13, 12.5),
        [System.Drawing.PointF]::new(5, 12.5)
    )
    $arrow.AddPolygon($pts)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(200, 10, 30, 60), 1.2)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $arrow)
    $g.FillPath([System.Drawing.Brushes]::White, $arrow)

    if ($paused) {
        # Pause-Zeichen unten links
        $g.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 200, 60, 40)), 0, 18, 13, 13)
        $g.FillRectangle([System.Drawing.Brushes]::White, 3.6, 21, 2.2, 7)
        $g.FillRectangle([System.Drawing.Brushes]::White, 7.2, 21, 2.2, 7)
    }

    $g.Dispose()
    return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)
    # BITMAPINFOHEADER (Höhe doppelt: XOR + AND-Maske)
    $bw.Write([int]40); $bw.Write([int]$w); $bw.Write([int]($h * 2))
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $maskStride = [int]([Math]::Ceiling($w / 32.0) * 4)
    for ($y = $h - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $maskStride
        for ($x = 0; $x -lt $w; $x++) {
            if ($bmp.GetPixel($x, $y).A -eq 0) { $row[[int][Math]::Floor($x / 8)] = $row[[int][Math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8)) }
        }
        $bw.Write($row)
    }
    $bw.Flush()
    return $ms.ToArray()
}

function Write-Ico([string]$path, [bool]$paused) {
    $sizes = 16, 20, 24, 32, 40, 48, 64, 256
    $images = foreach ($size in $sizes) {
        $bmp = New-IconBitmap $size $paused
        if ($size -ge 256) {
            $ms = [System.IO.MemoryStream]::new()
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            , $ms.ToArray()
        } else {
            , (Get-DibBytes $bmp)
        }
        $bmp.Dispose()
    }

    $fs = [System.IO.File]::Create($path)
    $bw = [System.IO.BinaryWriter]::new($fs)
    $bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([int16]1); $bw.Write([int16]32)
        $bw.Write([int]$images[$i].Length); $bw.Write([int]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write([byte[]]$img) }
    $bw.Dispose()
    Write-Host "Geschrieben: $path"
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
Write-Ico (Join-Path $OutDir 'app.ico') $false
Write-Ico (Join-Path $OutDir 'app-paused.ico') $true
