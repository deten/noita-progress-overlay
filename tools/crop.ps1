param([int]$X=1905,[int]$Y=78,[int]$W=600,[int]$H=960,[string]$Name='crop.png')
Add-Type -AssemblyName System.Drawing
$src = New-Object System.Drawing.Bitmap("$PSScriptRoot\..\dist\screen.png")
$X=[Math]::Max(0,$X); $Y=[Math]::Max(0,$Y)
$W=[Math]::Min($W,$src.Width-$X); $H=[Math]::Min($H,$src.Height-$Y)
$r = New-Object System.Drawing.Rectangle($X,$Y,$W,$H)
$out = $src.Clone($r, $src.PixelFormat)
$p = "$PSScriptRoot\..\dist\$Name"
$out.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "cropped $X,$Y ${W}x$H -> $p"
$src.Dispose(); $out.Dispose()
