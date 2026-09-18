param([int]$X, [int]$Y)
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
$mx = [math]::Floor($X/512) + $ox
$my = [math]::Floor($Y/512) + $offsetY
$p = $bmp.GetPixel($mx,$my)
$k = '{0:X2}{1:X2}{2:X2}' -f $p.R,$p.G,$p.B
$n = $c2b[$k]; if (-not $n) { $n = "(unmapped $k)" }
Write-Host ("world ({0},{1})  chunk ({2},{3})  map cell ({4},{5})" -f $X,$Y,[math]::Floor($X/512),[math]::Floor($Y/512),$mx,$my)
Write-Host ("biome: {0}" -f $n)
$bmp.Dispose()
