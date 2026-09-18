# Aggregates every past session file into a lifetime ledger.
$sess = "$env:USERPROFILE\AppData\LocalLow\Nolla_Games_Noita\save00\stats\sessions"
$biomes = @{}; $kills = @{}; $runs = 0; $deepest = 0; $totalPlay = 0.0
$best = $null
foreach ($f in Get-ChildItem "$sess\*_stats.xml") {
  try { $x = [xml](Get-Content $f.FullName -Raw) } catch { continue }
  $runs++
  $s = $x.Stats.stats
  $totalPlay += [double]$s.playtime
  $dy = [double]$s.'death_pos.y'
  if ($dy -gt $deepest) { $deepest = $dy; $best = $f.Name }
  foreach ($e in $x.Stats.biomes_visited.E) {
    if ($e.key) { $biomes[$e.key] = [int]$biomes[$e.key] + [int]$e.value }
  }
}
foreach ($f in Get-ChildItem "$sess\*_kills.xml") {
  try { $x = [xml](Get-Content $f.FullName -Raw) } catch { continue }
  foreach ($e in $x.Stats.kill_map.E) { if ($e.key) { $kills[$e.key] = [int]$kills[$e.key] + [int]$e.value } }
}
Write-Host "runs=$runs  totalPlaytime=$([math]::Round($totalPlay/3600,1))h  deepestDeathY=$deepest ($best)"
Write-Host ""
Write-Host "=== BIOMES EVER VISITED ($($biomes.Count)) ==="
$biomes.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { "{0,-32} {1}" -f $_.Key, $_.Value }
Write-Host ""
Write-Host "=== NOTABLE KILLS ==="
foreach ($k in @('boss_centipede','boss_dragon','boss_alchemist','boss_wizard','boss_limbs','boss_ghost','boss_pit','boss_robot','boss_meat','boss_fish','islandspirit','maggot','sun','dark_sun','minaraatti')) {
  if ($kills.ContainsKey($k)) { "{0,-24} {1}" -f $k, $kills[$k] }
}
Write-Host ""
Write-Host "=== total distinct creatures killed: $($kills.Count) ==="
