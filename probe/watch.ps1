# Noita save-write probe: detects WHICH files the game writes mid-run, and WHEN.
$ErrorActionPreference = 'SilentlyContinue'
$save = "$env:USERPROFILE\AppData\LocalLow\Nolla_Games_Noita\save00"
$log  = "$PSScriptRoot\probe_log.txt"
"=== probe start $(Get-Date -f 'HH:mm:ss') ===" | Out-File $log -Encoding utf8

$state = @{}
function Snap {
  $h = @{}
  foreach ($p in @("$save\stats\sessions","$save\persistent\flags","$save\world")) {
    Get-ChildItem $p -File -Recurse | ForEach-Object { $h[$_.FullName] = "$($_.Length)|$($_.LastWriteTime.Ticks)" }
  }
  foreach ($f in @("$save\world_state.xml","$save\player.xml","$save\session_numbers.salakieli","$save\stats\_stats.salakieli")) {
    $i = Get-Item $f; if ($i) { $h[$i.FullName] = "$($i.Length)|$($i.LastWriteTime.Ticks)" }
  }
  $h
}
function Biomes {
  $s = Get-ChildItem "$save\stats\sessions\*_stats.xml" | Sort-Object LastWriteTime -Desc | Select-Object -First 1
  if (-not $s) { return "(no session file)" }
  $x = [xml](Get-Content $s.FullName -Raw)
  $b = ($x.Stats.biomes_visited.E | ForEach-Object { $_.key }) -join ','
  "$($s.Name) playtime=$($x.Stats.stats.playtime_str) seed=$($x.Stats.stats.world_seed) hp=$($x.Stats.stats.hp) places=$($x.Stats.stats.places_visited) biomes=[$b]"
}

$state = Snap
"baseline: $($state.Count) files" | Out-File $log -Append -Encoding utf8
(Biomes) | Out-File $log -Append -Encoding utf8

$deadline = (Get-Date).AddMinutes(45)
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 2
  $new = Snap
  $changed = @()
  foreach ($k in $new.Keys) { if ($state[$k] -ne $new[$k]) { $changed += (Split-Path $k -Leaf) } }
  foreach ($k in $state.Keys) { if (-not $new.ContainsKey($k)) { $changed += "DEL:" + (Split-Path $k -Leaf) } }
  if ($changed.Count -gt 0) {
    $t = Get-Date -f 'HH:mm:ss'
    $worldChunks = ($changed | Where-Object { $_ -like 'world_*' }).Count
    $other = $changed | Where-Object { $_ -notlike 'world_*' }
    "[$t] chunks=$worldChunks other=$($other -join ' ')" | Out-File $log -Append -Encoding utf8
    if ($other -match '_stats.xml') { "         -> $(Biomes)" | Out-File $log -Append -Encoding utf8 }
    $state = $new
  }
}
"=== probe end ===" | Out-File $log -Append -Encoding utf8
