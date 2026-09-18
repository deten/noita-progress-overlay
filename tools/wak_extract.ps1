# Extracts named files from Noita's data.wak archive.
# Format: [u32 unk][u32 file_count][u32 data_start][u32 unk] then entries:
#         [u32 offset][u32 size][u32 name_len][name bytes]
param(
  [string]$Wak  = "C:\Program Files (x86)\Steam\steamapps\common\Noita\data\data.wak",
  [string]$Out  = "$PSScriptRoot\..\gamedata",
  [string[]]$Match = @('biome_impl/biome_map', '_biomes_all.xml', 'biome/', 'biome_map')
)
$fs = [System.IO.File]::OpenRead($Wak)
$br = New-Object System.IO.BinaryReader($fs)
$null = $br.ReadUInt32(); $count = $br.ReadUInt32(); $dataStart = $br.ReadUInt32(); $null = $br.ReadUInt32()
Write-Host "count=$count dataStart=$dataStart"

$entries = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt $count; $i++) {
  $off = $br.ReadUInt32(); $size = $br.ReadUInt32(); $nlen = $br.ReadUInt32()
  if ($nlen -gt 4096) { Write-Host "bad namelen at $i"; break }
  $name = [System.Text.Encoding]::ASCII.GetString($br.ReadBytes($nlen))
  $null = $entries.Add([pscustomobject]@{ Name=$name; Offset=$off; Size=$size })
}
Write-Host "parsed $($entries.Count) entries; table ended at $($fs.Position) (dataStart=$dataStart)"

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$hits = $entries | Where-Object { $n = $_.Name; ($Match | Where-Object { $n -like "*$_*" }).Count -gt 0 }
Write-Host "matched $($hits.Count) files"
foreach ($h in $hits) {
  $dest = Join-Path $Out ($h.Name -replace '^data/','' -replace '/','\')
  New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
  $fs.Position = $h.Offset
  [System.IO.File]::WriteAllBytes($dest, $br.ReadBytes($h.Size))
}
$br.Close(); $fs.Close()
Write-Host "done -> $Out"
