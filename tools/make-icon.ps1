# Рисует passgen.ico (белый замок на синем фоне). Запуск:
#   powershell -NoProfile -ExecutionPolicy Bypass -File make-icon.ps1 [-PreviewDir <папка для PNG>]
param([string]$PreviewDir = '')

Add-Type -AssemblyName System.Drawing

$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$out   = [IO.Path]::GetFullPath((Join-Path $here '..\src\passgen.ico'))
$sizes = 16, 20, 24, 32, 40, 48, 256

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function New-Frame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $u = [single]$s

    # фон: скруглённый квадрат с вертикальным градиентом
    $bgPath = New-RoundRect ($u * 0.04) ($u * 0.04) ($u * 0.92) ($u * 0.92) ($u * 0.22)
    $rect = New-Object System.Drawing.RectangleF 0, 0, $u, $u
    $c1 = [System.Drawing.Color]::FromArgb(255, 74, 144, 252)
    $c2 = [System.Drawing.Color]::FromArgb(255, 28, 72, 196)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 90
    $g.FillPath($grad, $bgPath)

    # дужка
    $penW = [Math]::Max(1.7, $u * 0.10)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $penW
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($pen, $u * 0.35, $u * 0.22, $u * 0.30, $u * 0.30, 180, 180)
    $g.DrawLine($pen, $u * 0.35, $u * 0.37, $u * 0.35, $u * 0.52)
    $g.DrawLine($pen, $u * 0.65, $u * 0.37, $u * 0.65, $u * 0.52)

    # корпус замка
    $body = New-RoundRect ($u * 0.25) ($u * 0.48) ($u * 0.50) ($u * 0.34) ($u * 0.07)
    $g.FillPath([System.Drawing.Brushes]::White, $body)

    # замочная скважина
    $hole = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 37, 90, 220))
    $g.FillEllipse($hole, $u * 0.445, $u * 0.57, $u * 0.11, $u * 0.11)
    $g.FillRectangle($hole, $u * 0.478, $u * 0.63, $u * 0.044, $u * 0.10)

    $g.Dispose()
    $bmp
}

# Кадр ICO в виде классического DIB (32 бита, BGRA) — читается любым софтом, включая System.Drawing.Icon
function Get-BmpFrame([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
    $bd = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $bd.Stride
    $raw = New-Object byte[] ($stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $raw, 0, $raw.Length)
    $bmp.UnlockBits($bd)

    $ms = New-Object System.IO.MemoryStream
    $w  = New-Object System.IO.BinaryWriter $ms
    $maskRow = [int]([Math]::Ceiling($s / 32.0)) * 4
    $w.Write([uint32]40)                                   # BITMAPINFOHEADER
    $w.Write([int32]$s)
    $w.Write([int32]($s * 2))                              # высота ×2: XOR-картинка + AND-маска
    $w.Write([uint16]1)
    $w.Write([uint16]32)
    $w.Write([uint32]0)
    $w.Write([uint32]($s * $s * 4 + $maskRow * $s))
    $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($raw, $y * $stride, $s * 4) }   # строки снизу вверх
    $w.Write((New-Object byte[] ($maskRow * $s)))          # AND-маска: всё непрозрачно, альфа берётся из BGRA
    $w.Flush()
    , $ms.ToArray()
}

function Get-PngFrame([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}

$frames = @()
foreach ($s in $sizes) {
    $bmp = New-Frame $s
    if ($PreviewDir) {
        New-Item -ItemType Directory -Force $PreviewDir | Out-Null
        $bmp.Save((Join-Path $PreviewDir "icon-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    # мелкие размеры — DIB (их берёт System.Drawing.Icon для трея), крупные — PNG (компактнее)
    if ($s -ge 48) { $data = Get-PngFrame $bmp } else { $data = Get-BmpFrame $bmp }
    $frames += [pscustomobject]@{ Size = $s; Data = $data }
    $bmp.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$f.Data.Length); $bw.Write([uint32]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f.Data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($out, $ms.ToArray())
"OK: $out ($((Get-Item $out).Length) байт)"
