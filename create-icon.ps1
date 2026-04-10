Add-Type -AssemblyName System.Drawing

$OutPath = "$PSScriptRoot\BaumConfigureGUI\Resources\app.ico"

$sizes = @(256, 64, 48, 32, 16)
$frames = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Background
    $g.Clear([System.Drawing.Color]::FromArgb(18, 18, 26))

    # Outer accent circle
    $accBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(78, 131, 253))
    $pad = [int]($s * 0.08)
    $g.FillEllipse($accBrush, $pad, $pad, ($s - $pad*2), ($s - $pad*2))

    # Inner dark circle
    $innerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(22, 22, 32))
    $ip = [int]($s * 0.22)
    $g.FillEllipse($innerBrush, $ip, $ip, ($s - $ip*2), ($s - $ip*2))

    # 'C' letter centered
    $fs   = [float]([int]($s * 0.40))
    $font = New-Object System.Drawing.Font("Segoe UI", $fs, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $wb   = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 230, 240))
    $sf   = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $s, $s)
    $g.DrawString("C", $font, $wb, $rect, $sf)

    $g.Dispose()

    # Encode as PNG into byte array
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

# Write ICO file manually (multi-size PNG-based ICO)
$ico = New-Object System.IO.MemoryStream
$count = $sizes.Count

# ICO header: reserved(2) + type=1(2) + count(2)
$ico.Write([byte[]](0,0,1,0,[byte]$count,0), 0, 6)

# Calculate data offset (after header + directory)
$offset = 6 + $count * 16
$entries = @()
for ($i = 0; $i -lt $count; $i++) {
    $s    = $sizes[$i]
    $data = $frames[$i]
    if ($s -eq 256) { $wh = 0 } else { $wh = [byte]$s }
    $entry  = [byte[]]($wh, $wh, 0, 0, 1, 0, 32, 0)
    $lenB   = [System.BitConverter]::GetBytes([int]$data.Length)
    $offB   = [System.BitConverter]::GetBytes([int]$offset)
    $entries += ,$entry
    $ico.Write($entry, 0, 8)
    $ico.Write($lenB,  0, 4)
    $ico.Write($offB,  0, 4)
    $offset += $data.Length
}

foreach ($data in $frames) {
    $ico.Write($data, 0, $data.Length)
}

[System.IO.File]::WriteAllBytes($OutPath, $ico.ToArray())
Write-Host "Icon written to $OutPath ($($ico.Length) bytes)"
