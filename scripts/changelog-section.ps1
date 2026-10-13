# Prints the release notes for a version: its section from CHANGELOG.md plus the install / verify tail.
#   powershell -File scripts/changelog-section.ps1 -Version 0.1.0 [-Tag v0.1.0-rc.1]
# Fails if CHANGELOG.md has no "## [v<Version>]" section, so a release cannot go out without notes.

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Tag = "v$Version"
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$lines = Get-Content CHANGELOG.md -Encoding UTF8
$start = ($lines | Select-String -Pattern "^## \[v$([regex]::Escape($Version))\]" | Select-Object -First 1).LineNumber
if (-not $start) { throw "CHANGELOG.md has no section '## [v$Version]'" }
$end = ($lines | Select-Object -Skip $start | Select-String -Pattern '^## ' | Select-Object -First 1).LineNumber
$body = if ($end) { $lines[$start..($start + $end - 2)] } else { $lines[$start..($lines.Count - 1)] }

$repo = 'kivilev/plsql-developer-text-tools'
$tail = @"

## Install

1. Close PL/SQL Developer (64-bit, version 12 or later; tested on 14).
2. Download ``TextTools.dll`` below and copy it into ``<PL/SQL Developer>\PlugIns``.
   From the zip: unpack, then ``Unblock-File TextTools.dll`` if Windows marked the download.
3. Start the IDE; if the Plug-Ins menu has no Text Tools, enable it in Configure > Plug-Ins.

No .NET runtime is needed: the DLL is compiled with .NET Native AOT.

## Verify the download

- Checksums: ``SHA256SUMS.txt`` (``Get-FileHash TextTools.dll -Algorithm SHA256``).
- Build provenance: ``gh attestation verify TextTools.dll -R $repo``.
"@

(($body -join "`n").Trim() + "`n" + $tail).Replace("{TAG}", $Tag)
