# Test in a real PL/SQL Developer (CI: .github/workflows/real-ide.yml, a throwaway Windows VM).
#   pwsh scripts/real-ide-smoke.ps1 -Msi plsqldev.msi -Dll PlsqlDevTextTools.dll -Out real-ide
# Installs the IDE silently, puts the plug-in into PlugIns, starts the IDE (not logged on) with TEXTTOOLS_SELFTEST set
# and gets it through the first-start dialogs (Oracle Client Wizard, logon, Register -> Trial). The plug-in then runs
# its self test (src/TextTools.Plugin/SelfTest.cs) through the real IDE callbacks and writes selftest.json.
# Fails when the IDE dies, the plug-in is not loaded, the self test does not finish or any check fails.
# Records the windows the IDE showed and screenshots. Never run it on a developer machine: it installs software,
# clicks through dialogs and kills the IDE.
param(
    [Parameter(Mandatory)] [string] $Msi,
    [Parameter(Mandatory)] [string] $Dll,
    [string] $Out = 'real-ide',
    [int] $TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$selfTest = Join-Path $Out 'selftest.json'
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
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc f, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    static string Text(IntPtr h) { var t = new StringBuilder(512); GetWindowText(h, t, 512); return t.ToString(); }
    static string Class(IntPtr h) { var c = new StringBuilder(256); GetClassName(h, c, 256); return c.ToString(); }
    static List<IntPtr> Top(int pid) {
        var list = new List<IntPtr>();
        EnumWindows((h, p) => { uint id; GetWindowThreadProcessId(h, out id); if (id == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    /// "Class | Title" of the visible top-level windows of the process.
    public static List<string> Of(int pid) {
        var list = new List<string>();
        foreach (var h in Top(pid)) list.Add(Class(h) + " | " + Text(h));
        return list;
    }
    /// WM_CLOSE to the visible top-level windows of the process with this class; returns how many.
    public static int Close(int pid, string className) {
        var n = 0;
        foreach (var h in Top(pid)) if (Class(h) == className) { PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); n++; }
        return n;
    }
    /// BM_CLICK on the child button with this caption ('&' ignored) in windows of this class; true if found.
    public static bool Click(int pid, string className, string caption) {
        var found = false;
        foreach (var h in Top(pid)) {
            if (Class(h) != className) continue;
            EnumChildWindows(h, (c, p) => {
                if (Text(c).Replace("&", "") == caption) { PostMessage(c, 0x00F5, IntPtr.Zero, IntPtr.Zero); found = true; return false; }
                return true;
            }, IntPtr.Zero);
        }
        return found;
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

function Save { $result | ConvertTo-Json -Depth 4 | Set-Content "$Out\result.json"; Get-Content "$Out\result.json" }

# 1. Silent install (a plain MSI).
$install = Start-Process msiexec.exe -ArgumentList "/i `"$((Resolve-Path $Msi).Path)`" /qn /norestart /l*v `"$Out\install.log`"" -Wait -PassThru
$result.msiExitCode = $install.ExitCode
$exe = Get-ChildItem "$env:ProgramFiles\PLSQL Developer*\plsqldev.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
$result.installed = [bool]$exe
if (-not $exe) { Save; throw "plsqldev.exe not found after install (msiexec exit $($install.ExitCode)), see install.log" }
$result.ideDir = $exe.DirectoryName
$result.ideVersion = $exe.VersionInfo.ProductVersion

# 2. The plug-in.
Copy-Item $Dll (Join-Path $exe.DirectoryName 'PlugIns') -Force

# 3. Start with the self test requested and walk through the first-start dialogs until the result file appears.
$result.oci = (Get-Command oci.dll -ErrorAction SilentlyContinue).Source
$env:TEXTTOOLS_SELFTEST = $selfTest
$ide = Start-Process $exe.FullName -PassThru
$result.dialogs = @()
$known = @('TPLSQLDevForm', 'TApplication')
$seen = @{}
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and -not (Test-Path $selfTest)) {
    Start-Sleep -Seconds 3
    $ide.Refresh()
    if ($ide.HasExited) { break }
    foreach ($w in [Win]::Of($ide.Id)) {
        $class = ($w -split ' \| ')[0]
        if ($known -contains $class) { continue }
        if (-not $seen.ContainsKey($class)) {
            $seen[$class] = 0
            Screenshot "$Out\dialog-$class.png"
            $result.dialogs += $w
        }
        $seen[$class]++
        switch ($class) {
            'TClientWizardForm' { [void][Win]::Close($ide.Id, $class) }        # no client configuration
            'TLogOnForm' { [void][Win]::Close($ide.Id, $class) }               # Cancel = not logged on
            'TRegisterForm' { if (-not [Win]::Click($ide.Id, $class, 'Trial')) { [void][Win]::Close($ide.Id, $class) } }
            default { if ($seen[$class] -ge 2) { [void][Win]::Close($ide.Id, $class) } }  # unknown: recorded, then closed
        }
    }
}

$ide.Refresh()
$result.alive = -not $ide.HasExited
if ($result.alive) {
    $result.pluginLoaded = [bool]($ide.Modules | Where-Object { $_.ModuleName -eq 'PlsqlDevTextTools.dll' })
    $result.windows = @([Win]::Of($ide.Id))
} else {
    $result.exitCode = $ide.ExitCode
}
Start-Sleep -Seconds 2
Screenshot "$Out\screen.png"
if ($result.alive) { Stop-Process -Id $ide.Id -Force }

$result.selfTest = Test-Path $selfTest
if ($result.selfTest) {
    $report = Get-Content $selfTest -Raw | ConvertFrom-Json
    $result.passed = $report.passed
    $result.failed = $report.failed
    $result.failures = @($report.checks | Where-Object { -not $_.ok } | ForEach-Object { "$($_.name): $($_.detail)" })
}
Save

if (-not $result.alive) { throw 'the IDE exited during start' }
if (-not $result.pluginLoaded) { throw 'the plug-in DLL is not loaded into the IDE' }
if (-not $result.selfTest) { throw "the self test did not finish in $TimeoutSeconds s (a dialog left open? see dialog-*.png)" }
if ($result.failed -gt 0) { throw "self test: $($result.failed) check(s) failed" }
