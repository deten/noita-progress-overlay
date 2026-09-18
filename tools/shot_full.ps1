# Captures the entire virtual screen, DPI-aware, so coordinate spaces cannot mismatch.
Add-Type @"
using System;using System.Runtime.InteropServices;
public class Dpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[void][Dpi]::SetProcessDPIAware()
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
Write-Host "virtual screen: $($vs.X),$($vs.Y) $($vs.Width)x$($vs.Height)"
$bmp = New-Object System.Drawing.Bitmap($vs.Width, $vs.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($vs.X, $vs.Y, 0, 0, $bmp.Size)
$out = "$PSScriptRoot\..\dist\screen.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "saved $out"
