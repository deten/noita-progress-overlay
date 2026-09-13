# For each biome, the actual world-coordinate extent, so hints can name real directions.
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

# canonical collapse, mirroring Model.Canon
function Canon($b) {
  if (-not $b) { return "" }
  if ($b -like 'temple_altar*' -or $b -like 'temple_wall*') { return 'holymountain' }
  if ($b -like 'orbrooms/*') { return 'orbroom' }
  if ($b -like 'tower/*')    { return 'tower' }
  if ($b -like 'pyramid*')   { return 'pyramid' }
  if ($b -like 'rainforest*'){ return 'rainforest' }
  if ($b -like 'lavalake*')  { return 'lavalake' }
  if ($b -like 'lava*')      { return 'lava' }
  if ($b -like 'snowcastle*'){ return 'snowcastle' }
  if ($b -like 'snowcave*')  { return 'snowcave' }
  if ($b -like 'excavationsite*') { return 'excavationsite' }
  if ($b -like 'wizardcave*'){ return 'wizardcave' }
  if ($b -like 'boss_arena*'){ return 'boss_arena' }
  if ($b -like 'essenceroom*'){ return 'essenceroom' }
  if ($b -like 'mountain_*' -or $b -eq 'hills' -or $b -eq 'hills2') { return 'surface' }
  if ($b -eq 'meatroom') { return 'meat' }
  if ($b -eq 'roboroom' -or $b -eq 'robot_egg') { return 'robobase' }
  if ($b -eq 'lake_statue' -or $b -eq 'lake_deep') { return 'lake' }
  if ($b -like 'data/*' -or $b -eq 'solid_wall' -or $b -eq 'empty') { return "" }
  return $b
}

$ext = @{}
for ($y=0; $y -lt $bmp.Height; $y++) {
  for ($x=0; $x -lt $bmp.Width; $x++) {
    $p = $bmp.GetPixel($x,$y); if ($p.A -eq 0) { continue }
    $k = '{0:X2}{1:X2}{2:X2}' -f $p.R,$p.G,$p.B
    $n = Canon $c2b[$k]; if (-not $n) { continue }
    $wx = ($x - $offsetX) * 512; $wy = ($y - $offsetY) * 512
    if (-not $ext.ContainsKey($n)) { $ext[$n] = @{ x0=$wx; x1=$wx+512; y0=$wy; y1=$wy+512; n=0 } }
    $e = $ext[$n]
    if ($wx -lt $e.x0) { $e.x0 = $wx }; if ($wx+512 -gt $e.x1) { $e.x1 = $wx+512 }
    if ($wy -lt $e.y0) { $e.y0 = $wy }; if ($wy+512 -gt $e.y1) { $e.y1 = $wy+512 }
    $e.n++
  }
}
$bmp.Dispose()

$want = @('surface','coalmine','coalmine_alt','excavationsite','fungicave','snowcave','snowcastle',
          'rainforest','vault','crypt','boss_arena','holymountain','wandcave','liquidcave','dragoncave',
          'orbroom','desert','pyramid','winter','winter_caves','lake','fungiforest','tower','clouds',
          'the_sky','meat','robobase','wizardcave','secret_lab','the_end','vault_frozen','sandcave','lavalake')
Write-Host ("{0,-16} {1,9} {2,9}   {3,8} {4,8}   {5}" -f 'PLACE','WEST-X','EAST-X','TOP-Y','BOT-Y','SIDE')
foreach ($k in $want) {
  if (-not $ext.ContainsKey($k)) { Write-Host ("{0,-16} (not on the biome map)" -f $k); continue }
  $e = $ext[$k]
  $mid = ($e.x0 + $e.x1) / 2
  $side = if ($mid -lt -1500) { 'WEST' } elseif ($mid -gt 1500) { 'EAST' } else { 'centre' }
  Write-Host ("{0,-16} {1,9} {2,9}   {3,8} {4,8}   {5}" -f $k,$e.x0,$e.x1,$e.y0,$e.y1,$side)
}
