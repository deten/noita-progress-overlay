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
$offsetX = [int]($bmp.Width/2)
$acc = @{}
for ($y=0; $y -lt $bmp.Height; $y++) {
  for ($x=0; $x -lt $bmp.Width; $x++) {
    $p = $bmp.GetPixel($x,$y); if ($p.A -eq 0) { continue }
    $k = '{0:X2}{1:X2}{2:X2}' -f $p.R,$p.G,$p.B
    $n = $c2b[$k]; if (-not $n) { continue }
    if ($n -notlike 'tower/*') { continue }
    $wx = ($x-$offsetX)*512; $wy = ($y-$offsetY)*512
    if (-not $acc.ContainsKey($n)) { $acc[$n] = @{x0=$wx;x1=$wx+512;y0=$wy;y1=$wy+512;n=0} }
    $e=$acc[$n]
    if($wx -lt $e.x0){$e.x0=$wx}; if($wx+512 -gt $e.x1){$e.x1=$wx+512}
    if($wy -lt $e.y0){$e.y0=$wy}; if($wy+512 -gt $e.y1){$e.y1=$wy+512}
    $e.n++
  }
}
$bmp.Dispose()
foreach ($k in ($acc.Keys | Sort-Object)) {
  $e=$acc[$k]
  Write-Host ("{0,-32} px={1,4}  x {2,7}..{3,-7} y {4,7}..{5,-7}" -f $k,$e.n,$e.x0,$e.x1,$e.y0,$e.y1)
}
