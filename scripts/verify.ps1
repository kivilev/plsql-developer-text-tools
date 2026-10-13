# Full local verification, the same gates as CI. Run from the repo root:
#   pwsh scripts/verify.ps1            (or: powershell -File scripts/verify.ps1)
# Stops at the first failing step. Requires .NET 10 SDK and VS Build Tools with the C++ workload (Native AOT linker).

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

# Native AOT looks for vswhere.exe on PATH.
$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $installer) { $env:PATH = "$installer;$env:PATH" }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Step([string]$name, [scriptblock]$action) {
    Write-Host "`n=== $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $name" -ForegroundColor Red; exit $LASTEXITCODE }
}

$dll = 'src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/TextTools.dll'

Step 'format (whitespace, style, analyzers)' { dotnet format TextTools.slnx --verify-no-changes }
Step 'build (warnings are errors)' { dotnet build TextTools.slnx -c Release }
Step 'unit tests + coverage gate' {
    dotnet test TextTools.slnx -c Release --no-build `
        /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
}
Step 'vulnerable packages' {
    $out = dotnet list TextTools.slnx package --vulnerable --include-transitive 2>&1 | Out-String
    Write-Host $out
    if ($out -match 'has the following vulnerable packages') { $global:LASTEXITCODE = 1 } else { $global:LASTEXITCODE = 0 }
}
Step 'publish Native AOT DLL' { dotnet publish src/TextTools.Plugin -c Release }
Step 'exports present' {
    $exports = 'IdentifyPlugIn', 'RegisterCallback', 'CreateMenuItem', 'OnMenuClick', 'OnActivate', 'PlugInName', 'About'
    $dumpbin = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\VC\Tools\MSVC\*\bin\Hostx64\x64\dumpbin.exe", `
        "$env:ProgramFiles\Microsoft Visual Studio\*\*\VC\Tools\MSVC\*\bin\Hostx64\x64\dumpbin.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    $text = & $dumpbin.FullName /exports $dll | Out-String
    $missing = $exports | Where-Object { $text -notmatch "\b$_\b" }
    if ($missing) { Write-Host "missing exports: $missing"; $global:LASTEXITCODE = 1 } else { Write-Host 'all exports present'; $global:LASTEXITCODE = 0 }
}
Step 'host emulator: smoke, nocallbacks, noutf8, unload, stress' {
    dotnet run --project tools/HostEmulator -c Release -- $dll all
}

Write-Host "`nALL CHECKS PASSED" -ForegroundColor Green
