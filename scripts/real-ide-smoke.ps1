# Smoke test in a real PL/SQL Developer (CI: .github/workflows/real-ide.yml, a throwaway Windows VM).
#   pwsh scripts/real-ide-smoke.ps1 -Msi plsqldev.msi -Dll PlsqlDevTextTools.dll -Out real-ide
# Installs the IDE silently, puts the plug-in into PlugIns, starts the IDE (no Oracle client, not logged on) and checks:
# the IDE stays alive, the plug-in DLL is loaded into it. Also records the top-level windows of the IDE (trial or
# first-start dialogs) and a screenshot, so a human can see what the IDE showed. Never run it on a developer machine:
# it installs software and kills the IDE.
param(
    [Parameter(Mandatory)] [string] $Msi,
    [Parameter(Mandatory)] [string] $Dll,
    [string] $Out = 'real-ide',
    [int] $StartSeconds = 90
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$result = [ordered]@{}

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Win {
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    /// Asks the visible top-level windows of the process with this class name to close (WM_CLOSE); returns how many.
    public static int Close(int pid, string className) {
        var n = 0;
        EnumWindows((h, p) => {
            uint id; GetWindowThreadProcessId(h, out id);
            var c = new StringBuilder(256); GetClassName(h, c, 256);
            if (id == pid && IsWindowVisible(h) && c.ToString() == className) { PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); n++; }
            return true;
        }, IntPtr.Zero);
        return n;
    }
    public static List<string> Of(int pid) {
        var list = new List<string>();
        EnumWindows((h, p) => {
            uint id; GetWindowThreadProcessId(h, out id);
            if (id == pid && IsWindowVisible(h)) {
                var t = new StringBuilder(512); GetWindowText(h, t, 512);
                var c = new StringBuilder(256); GetClassName(h, c, 256);
                list.Add(c + " | " + t);
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
'@

function Screenshot([string] $file) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

# 1. Silent install (a plain MSI).
$install = Start-Process msiexec.exe -ArgumentList "/i `"$((Resolve-Path $Msi).Path)`" /qn /norestart /l*v `"$Out\install.log`"" -Wait -PassThru
$result.msiExitCode = $install.ExitCode
$exe = Get-ChildItem "$env:ProgramFiles\PLSQL Developer*\plsqldev.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
$result.installed = [bool]$exe
if (-not $exe) {
    $result | ConvertTo-Json | Set-Content "$Out\result.json"
    throw "plsqldev.exe not found after install (msiexec exit $($install.ExitCode)), see install.log"
}
$result.ideDir = $exe.DirectoryName
$result.ideVersion = $exe.VersionInfo.ProductVersion

# 2. The plug-in.
Copy-Item $Dll (Join-Path $exe.DirectoryName 'PlugIns') -Force

# 3. Start and wait for the plug-in to load. First-start dialogs that block it (the Oracle Client Wizard when no
#    client is found) are closed and recorded.
$result.oci = (Get-Command oci.dll -ErrorAction SilentlyContinue).Source
$ide = Start-Process $exe.FullName -PassThru
$result.closedDialogs = @()
$deadline = (Get-Date).AddSeconds($StartSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $ide.Refresh()
    if ($ide.HasExited) { break }
    $windows = @([Win]::Of($ide.Id))
    if (-not $result.firstWindows -and $windows.Count -gt 0) { $result.firstWindows = $windows }
    # First start: the Oracle Client Wizard, then the logon dialog (closing it = Cancel = "Not logged on").
    foreach ($dialog in @(@{ Class = 'TClientWizardForm'; Name = 'client-wizard' }, @{ Class = 'TLogOnForm'; Name = 'logon' })) {
        if ([Win]::Of($ide.Id) -match "^$($dialog.Class) ") {
            if ($result.closedDialogs -notcontains $dialog.Name) { Screenshot "$Out\$($dialog.Name).png" }
            [void][Win]::Close($ide.Id, $dialog.Class)
            $result.closedDialogs += $dialog.Name
        }
    }
    if ($ide.Modules | Where-Object { $_.ModuleName -eq 'PlsqlDevTextTools.dll' }) { Start-Sleep -Seconds 5; break }
}
$ide.Refresh()
$result.alive = -not $ide.HasExited
if ($result.alive) {
    $result.pluginLoaded = [bool]($ide.Modules | Where-Object { $_.ModuleName -eq 'PlsqlDevTextTools.dll' })
    $result.windows = @([Win]::Of($ide.Id))
} else {
    $result.exitCode = $ide.ExitCode
}
Screenshot "$Out\screen.png"

if ($result.alive) { Stop-Process -Id $ide.Id -Force }
$result | ConvertTo-Json -Depth 3 | Set-Content "$Out\result.json"
Get-Content "$Out\result.json"

if (-not $result.alive) { throw 'the IDE exited during start' }
if (-not $result.pluginLoaded) { throw 'the plug-in DLL is not loaded into the IDE' }
