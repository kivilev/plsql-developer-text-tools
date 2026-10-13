# Packs a published PlsqlDevTextTools.dll into release assets. Used by .github/workflows/release.yml and locally (skill `release`).
#   powershell -File scripts/package.ps1 -Version 0.1.0          (after: dotnet publish src/TextTools.Plugin -c Release)
# Output in dist/: PlsqlDevTextTools.dll, PlsqlDevTextTools-<version>-win-x64.zip (DLL + README + LICENSE + THIRD-PARTY-NOTICES), SHA256SUMS.txt

param(
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$dll = 'src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/PlsqlDevTextTools.dll'
if (-not (Test-Path $dll)) { throw "$dll not found: run 'dotnet publish src/TextTools.Plugin -c Release' first" }

$dist = 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory $dist | Out-Null

# Bare DLL: the simplest install (copy into <PL/SQL Developer>\PlugIns).
Copy-Item $dll "$dist/PlsqlDevTextTools.dll"

# Zip with the documents a binary distribution must carry (MIT notices of this project and of the .NET runtime).
$staging = Join-Path $dist 'staging'
New-Item -ItemType Directory $staging | Out-Null
Copy-Item $dll, 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.txt' $staging
$zip = "$dist/PlsqlDevTextTools-$Version-win-x64.zip"
Compress-Archive -Path "$staging/*" -DestinationPath $zip
Remove-Item $staging -Recurse -Force

# Checksums in sha256sum format: "<hash>  <file>".
$sums = Get-ChildItem $dist -File | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
# LF line endings: `sha256sum -c` on Linux and Git Bash rejects CRLF.
[IO.File]::WriteAllText((Join-Path (Resolve-Path $dist) 'SHA256SUMS.txt'), ($sums -join "`n") + "`n")

Get-ChildItem $dist | Format-Table Name, Length
Get-Content "$dist/SHA256SUMS.txt"
