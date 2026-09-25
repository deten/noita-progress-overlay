# Compiles and runs the self-checking tests. Exits non-zero if any fail.
#   PrngVectors    the procedural RNG against published test vectors
#   ModifierCheck  biome modifier prediction against published and observed worlds
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$csc  = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out  = "$root\dist"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Xml.dll','/reference:System.Drawing.dll')

$tests = @(
  @{ Name = 'PrngVectors';   Src = @('src\Prng.cs', 'test\PrngVectors.cs') },
  @{ Name = 'ModifierCheck'; Src = @('src\Prng.cs', 'src\Modifiers.cs', 'src\BiomeModifiers.cs', 'test\ModifierCheck.cs') }
)

$failed = 0
foreach ($t in $tests) {
  $exe = "$out\$($t.Name).exe"
  $src = $t.Src | ForEach-Object { Join-Path $root $_ }
  & $csc /nologo /target:exe "/main:NoitaOverlay.$($t.Name)" "/out:$exe" @refs @src
  if ($LASTEXITCODE -ne 0) { Write-Host "COMPILE FAILED: $($t.Name)"; $failed++; continue }
  Write-Host "----- $($t.Name) -----"
  & $exe
  if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $($t.Name)"; $failed++ }
  Write-Host ""
}

if ($failed -eq 0) { Write-Host "all tests passed" } else { Write-Host "$failed test(s) failed" }
exit $failed
