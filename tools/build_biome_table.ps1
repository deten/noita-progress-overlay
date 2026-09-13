# Decodes biome_map.png + _biomes_all.xml into a world-coordinate -> biome lookup table.
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$png  = "$root\gamedata\biome_impl\biome_map.png"
$xml  = [xml](Get-Content "$root\gamedata\biome\_biomes_all.xml" -Raw)

$offsetY = [int]$xml.BiomesToLoad.biome_offset_y
$color2biome = @{}
foreach ($b in $xml.BiomesToLoad.Biome) {
  $c = $b.color.ToUpper()          # stored as AARRGGBB hex
  if ($c.Length -eq 8) { $c = $c.Substring(2) }   # drop alpha -> RRGGBB
  $name = ($b.biome_filename -replace '^data/biome/','' -replace '\.xml$','')
  if (-not $color2biome.ContainsKey($c)) { $color2biome[$c] = $name }
}
Write-Host "offsetY=$offsetY  colors=$($color2biome.Count)"

$bmp = New-Object System.Drawing.Bitmap($png)
Write-Host "biome_map.png = $($bmp.Width) x $($bmp.Height)"

# Count distinct colors present and resolve them
$seen = @{}
for ($y=0; $y -lt $bmp.Height; $y++) {
  for ($x=0; $x -lt $bmp.Width; $x++) {
    $p = $bmp.GetPixel($x,$y)
    if ($p.A -eq 0) { continue }
    $k = '{0:X2}{1:X2}{2:X2}' -f $p.R,$p.G,$p.B
    if (-not $seen.ContainsKey($k)) { $seen[$k] = @{ n=0; rows=@{} } }
    $seen[$k].n++
    $seen[$k].rows[$y] = $true
  }
}
Write-Host "distinct colors in map = $($seen.Count)"
Write-Host ""
Write-Host ("{0,-8} {1,-26} {2,7}  {3}" -f 'COLOR','BIOME','PIXELS','WORLD-Y RANGE')
foreach ($k in ($seen.Keys | Sort-Object { ($seen[$_].rows.Keys | Measure-Object -Minimum).Minimum })) {
  $rows = $seen[$k].rows.Keys | Sort-Object
  $y0 = ($rows | Select-Object -First 1); $y1 = ($rows | Select-Object -Last 1)
  $wy0 = ($y0 - $offsetY) * 512; $wy1 = ($y1 - $offsetY + 1) * 512
  $nm = $color2biome[$k]; if (-not $nm) { $nm = '(unmapped)' }
  Write-Host ("{0,-8} {1,-26} {2,7}  y {3} .. {4}" -f $k,$nm,$seen[$k].n,$wy0,$wy1)
}
$bmp.Dispose()
