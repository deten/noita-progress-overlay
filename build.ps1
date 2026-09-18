# Builds dist\NoitaOverlay.exe using the in-box .NET Framework compiler (no SDK install required).
$ErrorActionPreference = 'Stop'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

$src      = Get-ChildItem "$PSScriptRoot\src\*.cs" | ForEach-Object { $_.FullName }
$out      = "$PSScriptRoot\dist\NoitaOverlay.exe"
$manifest = "$PSScriptRoot\src\app.manifest"
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\dist" | Out-Null

$refs = @('System.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Core.dll')
$cscArgs = @('/nologo','/target:winexe','/optimize+','/warn:3',"/out:$out","/win32manifest:$manifest") +
           ($refs | ForEach-Object { "/reference:$_" }) + $src

Write-Host "compiling $($src.Count) files -> $out"
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Write-Host "OK: $out  ($([math]::Round((Get-Item $out).Length/1KB,1)) KB)"
