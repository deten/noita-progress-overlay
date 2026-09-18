param([string]$Name)
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$xml  = [xml](Get-Content "$root\gamedata\biome\_biomes_all.xml" -Raw)
$offsetY = [int]$xml.BiomesToLoad.biome_offset_y
$c2b = @{}
foreach ($b in $xml.BiomesToLoad.Biome) {
  $c = $b.color.ToUpper(); if ($c.Length -eq 8) { $c = $c.Substring(2) }
  if (-not $c2b.ContainsKey($c)) { $c2b[$c] = ($b.biome_filename -replace '^data/biome/','' -replace '\.xml$','') }
}
$bmp = New-Object System.Drawing.Bitmap("$root\gamedata\biome_impl\biome_map.png")
$ox = [int]($bmp.Width/2)
for ($y=0; $y -lt $bmp.Height; $y++) {
  for ($x=0; $x -lt $bmp.Width; $x++) {
    $p = $bmp.GetPixel($x,$y); if ($p.A -eq 0) { continue }
    $k = '{0:X2}{1:X2}{2:X2}' -f $p.R,$p.G,$p.B
    if ($c2b[$k] -ne $Name) { continue }
    $wx = ($x-$ox)*512; $wy = ($y-$offsetY)*512
    Write-Host ("{0}: world x {1}..{2}  y {3}..{4}   chunk ({5},{6})" -f $Name,$wx,($wx+512),$wy,($wy+512),[math]::Floor($wx/512),[math]::Floor($wy/512))
  }
}
$bmp.Dispose()
