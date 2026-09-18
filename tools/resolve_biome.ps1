# Resolves the player's current biome from the most recently written world chunk files.
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

function Get-Biome([int]$wx, [int]$wy) {
  $mx = [math]::Floor($wx / 512) + $offsetX
  $my = [math]::Floor($wy / 512) + $offsetY
  if ($mx -lt 0 -or $my -lt 0 -or $mx -ge $bmp.Width -or $my -ge $bmp.Height) { return "(out of map)" }
  $p = $bmp.GetPixel($mx, $my)
  $k = '{0:X2}{1:X2}{2:X2}' -f $p.R,$p.G,$p.B
  $n = $c2b[$k]; if (-not $n) { $n = "(unmapped $k)" }
  "$n   [map $mx,$my]"
}

$save = "$env:USERPROFILE\AppData\LocalLow\Nolla_Games_Noita\save00"
$chunks = Get-ChildItem "$save\world\world_*.png_petri" | Sort-Object LastWriteTime -Descending | Select-Object -First 8
Write-Host ("offsetX={0} offsetY={1}  map={2}x{3}" -f $offsetX,$offsetY,$bmp.Width,$bmp.Height)
Write-Host ""
Write-Host ("{0,-22} {1,-9} {2}" -f 'CHUNK','WRITTEN','BIOME')
foreach ($c in $chunks) {
  if ($c.Name -match 'world_(-?\d+)_(-?\d+)\.png_petri') {
    $wx = [int]$matches[1]; $wy = [int]$matches[2]
    Write-Host ("{0,-22} {1,-9} {2}" -f "$wx,$wy", $c.LastWriteTime.ToString('HH:mm:ss'), (Get-Biome $wx $wy))
  }
}
$bmp.Dispose()
