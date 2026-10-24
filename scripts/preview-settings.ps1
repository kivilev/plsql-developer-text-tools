# Opens the Format Tools settings window from the built DLL, without PL/SQL Developer (host emulator).
#   powershell -ExecutionPolicy Bypass -File scripts/preview-settings.ps1 [-Build] [-Tab 0..5] [-Minutes 10]
# The window stays open for -Minutes (close it with Close earlier); -Build publishes the plug-in first.
param([switch]$Build, [int]$Tab = 0, [int]$Minutes = 10)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if ($Build) {
    $env:PATH += ";${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    dotnet publish src/TextTools.Plugin -c Release | Out-Null
}
$env:SETTINGS_HOLD_MS = [string]($Minutes * 60000)
$env:SETTINGS_TAB = [string]$Tab
dotnet run --project tools/HostEmulator -c Release -- "$PWD\src\TextTools.Plugin\bin\Release\net10.0\win-x64\publish\PlsqlDevTextTools.dll" settings
