# Resolves orb locations to biomes, using the decoded biome map.
# Orb positions are fixed per world (not seed-random): the orb rooms are literal cells
# in biome_map.png, and ORB_MAP_STRING in the save lists their chunk coordinates.
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
$offsetX = [int]($bmp.Width / 2)

function BiomeAt([int]$wx, [int]$wy) {
  $mx = [math]::Floor($wx / 512) + $offsetX
  $my = [math]::Floor($wy / 512) + $offsetY
  if ($mx -lt 0 -or $my -lt 0 -or $mx -ge $bmp.Width -or $my -ge $bmp.Height) { return '(off map)' }
  $p = $bmp.GetPixel($mx, $my)
  $k = '{0:X2}{1:X2}{2:X2}' -f $p.R, $p.G, $p.B
  $n = $c2b[$k]
  if (-not $n) { return '(unmapped)' }
  return $n
}

# Captured from world_state.xml earlier this session; re-read it when the game is not running.
$orbs = '8,1 1,-3 -9,7 -8,19 -18,28 -20,5 -1,31 20,31 19,5 6,3 19,-3'
$ws = "$env:USERPROFILE\AppData\LocalLow\Nolla_Games_Noita\save00\world_state.xml"
if (Test-Path $ws) {
  $raw = Get-Content $ws -Raw
  if ($raw -match 'ORB_MAP_STRING[\s\S]{0,80}?value="([^"]+)"') { $orbs = $matches[1] }
}
Write-Host "ORB_MAP_STRING = $orbs"
Write-Host ""

$have = @{}
$orbDir = "$env:USERPROFILE\AppData\LocalLow\Nolla_Games_Noita\save00\persistent\orbs_new"
if (Test-Path $orbDir) {
  Get-ChildItem $orbDir -File | ForEach-Object {
    $n = 0; if ([int]::TryParse($_.Name, [ref]$n)) { $have[$n] = $true }
  }
}

$i = 0
Write-Host ("{0,-4} {1,9} {2,9}   {3,-30} {4}" -f 'ORB', 'WORLD-X', 'WORLD-Y', 'BIOME', 'YOURS?')
foreach ($pair in ($orbs -split ' ')) {
  if ($pair -notmatch '^(-?\d+),(-?\d+)$') { continue }
  $wx = [int]$matches[1] * 512
  $wy = [int]$matches[2] * 512
  $mark = ''
  if ($have.ContainsKey($i)) { $mark = '<-- HAVE' }
  Write-Host ("{0,-4} {1,9} {2,9}   {3,-30} {4}" -f $i, $wx, $wy, (BiomeAt $wx $wy), $mark)
  $i++
}
$bmp.Dispose()
Write-Host ""
Write-Host "orbs collected (lifetime): $($have.Keys.Count) -> ids $(($have.Keys | Sort-Object) -join ', ')"
