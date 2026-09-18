# Full-screen capture, downscaled, so the whole desktop is visible at once.
Add-Type @"
using System;using System.Runtime.InteropServices;
public class Dpi2 { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[void][Dpi2]::SetProcessDPIAware()
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$full = New-Object System.Drawing.Bitmap($vs.Width, $vs.Height)
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($vs.X, $vs.Y, 0, 0, $full.Size)
$g.Dispose()
$scale = 1000 / $vs.Width
$w = [int]($vs.Width * $scale); $h = [int]($vs.Height * $scale)
$small = New-Object System.Drawing.Bitmap($w, $h)
$g2 = [System.Drawing.Graphics]::FromImage($small)
$g2.InterpolationMode = 'HighQualityBicubic'
$g2.DrawImage($full, 0, 0, $w, $h)
$g2.Dispose()
$out = "$PSScriptRoot\..\dist\desktop.png"
$small.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$full.Dispose(); $small.Dispose()
Write-Host "saved $out (${w}x$h from $($vs.Width)x$($vs.Height))"
