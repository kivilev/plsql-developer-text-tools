# Review: Real IDE Smoke Test & Self-Test Spike

Review of changes in `SPIKE.diff` (`.github/workflows/real-ide.yml`, `scripts/real-ide-smoke.ps1`, `src/TextTools.Plugin/SelfTest.cs`, `Exports.cs`, `Ide.cs`, `Clipboard.cs`, and `tools/HostEmulator`).

---

## 1. Concrete Issues

### [CRITICAL] `IdeIsIdle()` treats missing `TPLSQLDevForm` as idle, risking premature execution on an uninitialized IDE
- **File:Line:** [SelfTest.cs:490](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/SelfTest.cs#L490)
- **Why:** `return main == 0 || (IsWindowVisible(main) && IsWindowEnabled(main));`. If the timer fires while the IDE is starting up before `TPLSQLDevForm` is instantiated (e.g. during splash screen or early initialization), `main` is `0`, which evaluates to `true`. `OnTimer` immediately kills the timer and invokes `Run()` before Delphi's main form and MDI/docking infrastructure exist. Calling callback 20 (`IDE_CreateWindow`) against an uninitialized IDE can cause Delphi Access Violations and crash the process. This condition was added as a shortcut for `HostEmulator`.
- **Concrete Fix:** In `SelfTest.cs`, require `main != 0 && IsWindowVisible(main) && IsWindowEnabled(main)`. Discriminate `HostEmulator` either by `Ide.AppHandle == 0` or by creating a mock `TPLSQLDevForm` window inside `HostEmulator`:
  ```csharp
  private static bool IdeIsIdle()
  {
      nint main = 0;
      EnumWindows(&FindMain, (nint)(&main));
      if (Ide.AppHandle == 0) return true; // HostEmulator fallback
      return main != 0 && IsWindowVisible(main) && IsWindowEnabled(main);
  }
  ```

---

### [CRITICAL] Unhandled exceptions escaping `[UnmanagedCallersOnly]` callback `FindMain` crash the IDE via FailFast
- **File:Line:** [SelfTest.cs:493-503](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/SelfTest.cs#L493-L503)
- **Why:** `FindMain` is marked `[UnmanagedCallersOnly]` and called by Win32 `EnumWindows`. In .NET Native AOT, any exception that escapes an unmanaged entry point immediately terminates the process via `Environment.FailFast`. `FindMain` has no `try/catch`. It also dereferences `*(nint*)state = hwnd;` without checking `if (state == 0)`. Furthermore, `new string(name, 0, n)` performs a managed heap allocation on every top-level window of the process. This violates Rule #1 of `SKILL.md` ("No exception may leave an export").
- **Concrete Fix:** Wrap the callback body in `try/catch`, check `state`, and use span comparisons without heap allocation:
  ```csharp
  [UnmanagedCallersOnly]
  private static int FindMain(nint hwnd, nint state)
  {
      try
      {
          if (state == 0) return 0;
          GetWindowThreadProcessId(hwnd, out var pid);
          if (pid != (uint)Environment.ProcessId) return 1;
          var name = stackalloc char[64];
          var n = GetClassNameW(hwnd, name, 64);
          if (n <= 0 || !new ReadOnlySpan<char>(name, n).SequenceEqual("TPLSQLDevForm")) return 1;
          *(nint*)state = hwnd;
          return 0;
      }
      catch
      {
          return 1;
      }
  }
  ```

---

### [MAJOR] Missing reentrancy guard in `OnTimer` allows concurrent or nested `Run()` execution
- **File:Line:** [SelfTest.cs:368-386](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/SelfTest.cs#L368-L386)
- **Why:** Win32 `KillTimer` does not remove `WM_TIMER` messages already posted to the thread message queue. In addition, `_timer` is not reset to `0` after `KillTimer`. If `Ide.CreateWindow` or editor callbacks trigger a message dispatch loop (`Application.ProcessMessages`) or a modal message box, or if a user triggers `FromCommandWindow` while the timer is pending, `Run()` will be entered reentrantly. Multiple instances of `Run()` will simultaneously modify `Ide.SetText`, overwrite the clipboard, and corrupt test results.
- **Concrete Fix:** Add an atomic reentrancy guard and reset `_timer`:
  ```csharp
  private static int _running;

  [UnmanagedCallersOnly]
  private static void OnTimer(nint hwnd, uint message, nuint id, uint time)
  {
      try
      {
          var path = Environment.GetEnvironmentVariable(EnvironmentVariable) ?? "";
          if (++_ticks < MaxTicks && !IdeIsIdle()) return;
          if (_timer != 0) { KillTimer(0, _timer); _timer = 0; }
          if (_ticks >= MaxTicks)
          {
              Write(path, [new Check("ide ready", false, "the IDE main window never became enabled (a modal dialog?)")]);
              return;
          }
          if (Interlocked.Exchange(ref _running, 1) != 0) return;
          try { Write(path, Run()); }
          finally { _running = 0; }
      }
      catch { }
  }
  ```

---

### [MAJOR] `Clipboard.GetText` lacks retry loop, causing intermittent race conditions with clipboard monitors
- **File:Line:** [Clipboard.cs:13-28](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/Clipboard.cs#L13-L28)
- **Why:** `Clipboard.SetText` includes a retry loop (`Attempts = 10`, `Thread.Sleep(20)`) because external processes (clipboard managers, RDP `rdpclip`, Windows clipboard history) lock the clipboard upon update. `Clipboard.GetText` has zero retries. In `SelfTest.Copy`, `Clipboard.GetText` is called immediately after `Editor.Apply(cmd)` calls `SetText`. When a clipboard listener locks the clipboard in response to `WM_CLIPBOARDUPDATE`, `GetText` returns `null`, causing `SelfTest.Copy` to fail with `"<clipboard unreadable>"`.
- **Concrete Fix:** Apply retry logic to `OpenClipboard` in `GetText`:
  ```csharp
  public static string? GetText(nint owner)
  {
      var opened = OpenClipboard(owner);
      for (var i = 1; i < Attempts && !opened; i++)
      {
          Thread.Sleep(20);
          opened = OpenClipboard(owner);
      }
      if (!opened) return null;
      try
      {
          var handle = GetClipboardData(CfUnicodeText);
          var p = handle == 0 ? 0 : GlobalLock(handle);
          if (p == 0) return null;
          try { return Marshal.PtrToStringUni(p); }
          finally { GlobalUnlock(handle); }
      }
      finally { CloseClipboard(); }
  }
  ```

---

### [MAJOR] Unhandled exception accessing `$ide.Modules` in PowerShell leaks orphan `plsqldev.exe` and drops test results
- **File:Line:** [real-ide-smoke.ps1:211](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/scripts/real-ide-smoke.ps1#L211)
- **Why:** `Process.Modules` calls Win32 `EnumProcessModules`. On Windows, if process elevation differs or if the process exits concurrently, accessing `$ide.Modules` throws `System.ComponentModel.Win32Exception` (Access is denied) or `InvalidOperationException`. Because `$ErrorActionPreference = 'Stop'`, the script aborts at line 211. Line 218 (`Stop-Process`) is never reached, leaving `plsqldev.exe` running in the background, and line 227 (`Save`) is skipped.
- **Concrete Fix:** Wrap module inspection in `try/catch` and ensure process cleanup is inside a `finally` block:
  ```powershell
  try {
      # ... main dialog and test loop ...
      $result.alive = -not $ide.HasExited
      if ($result.alive) {
          try {
              $result.pluginLoaded = [bool]($ide.Modules | Where-Object { $_.ModuleName -eq 'PlsqlDevTextTools.dll' })
          } catch {
              $result.pluginLoaded = [bool]$result.selfTest
          }
          $result.windows = @([Win]::Of($ide.Id))
      }
  } finally {
      if ($ide -and -not $ide.HasExited) { Stop-Process -Id $ide.Id -Force }
  }
  ```

---

### [MAJOR] `FromCommandWindow` (`CommandLine` export) destroys user clipboard and allows unhandled path exceptions
- **File:Line:** [SelfTest.cs:58-66](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/SelfTest.cs#L58-L66), [Exports.cs:115-127](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/Exports.cs#L115-L127)
- **Why:** If an end-user runs `TextTools selftest` in the Command Window, `Run()` executes, opening a SQL Window and clearing/overwriting the clipboard (`Clipboard.SetText(Ide.AppHandle, "")`) without saving prior content or warning the user. Furthermore, if the user enters an invalid file path (e.g. `TextTools selftest C:\invalid:dir\test.json`), `Write(path, checks)` throws `ArgumentException`. Because `FromCommandWindow` does not catch it, `Exports.Guard` catches it and pops up a modal `MessageBoxW` in the IDE.
- **Concrete Fix:** Catch file writing errors in `FromCommandWindow` and report back via `Ide.CommandFeedback`. Preserve and restore existing clipboard text in `Copy()`:
  ```csharp
  var previousClipboard = Clipboard.GetText(Ide.AppHandle);
  try { /* run Copy test */ }
  finally { if (previousClipboard is not null) Clipboard.SetText(Ide.AppHandle, previousClipboard); }
  ```

---

### [MINOR] `SelfTest.Transform` lacks newline normalization, risking false failures across IDE versions
- **File:Line:** [SelfTest.cs:439](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/SelfTest.cs#L439)
- **Why:** `Same(actual, expected)` performs strict ordinal comparison (`string.Equals(a, b, StringComparison.Ordinal)`). `actual` is read from `Ide.GetText()` (which Delphi SynEdit normalizes to `\r\n`), while `expected` is generated directly by `cmd.Transform(input)`. If a transform outputs `\n`, the comparison fails despite identical text content. Lines 410 and 415 use `Normalize(...)`, but `Transform` omitted it.
- **Concrete Fix:** Normalize line endings before comparing:
  ```csharp
  var ok = Same(Normalize(actual), Normalize(expected));
  return new Check(cmd.Name, ok, ok ? Short(actual) : $"expected {Short(expected)}, got {Short(actual)}");
  ```

---

### [MINOR] `[Win]::Click` uses `PostMessage(BM_CLICK)` which Delphi VCL can drop, and loop lacks retry limit for `TRegisterForm`
- **File:Line:** [real-ide-smoke.ps1:146, 202](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/scripts/real-ide-smoke.ps1#L146)
- **Why:** `BM_CLICK` (0x00F5) is designed for synchronous invocation via `SendMessage`. `PostMessage` does not guarantee button state transition and is frequently ignored by Delphi VCL forms. Furthermore, if `[Win]::Click` matches the "Trial" button and posts `BM_CLICK`, but the dialog does not dismiss, line 202 executes `Click` on every loop iteration, bypassing the `default` fallback (`Close`), causing the script to hang until the 240s timeout.
- **Concrete Fix:** Use `SendMessage` for `BM_CLICK`, and add a fallback to close `TRegisterForm` if it remains visible after 2 attempts:
  ```csharp
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  // In Click: SendMessage(c, 0x00F5, IntPtr.Zero, IntPtr.Zero);
  ```
  ```powershell
  'TRegisterForm' {
      if ($seen[$class] -ge 3 -or -not [Win]::Click($ide.Id, $class, 'Trial')) {
          [void][Win]::Close($ide.Id, $class)
      }
  }
  ```

---

### [MINOR] Unsanitized window class name in screenshot path can crash the script on special characters
- **File:Line:** [real-ide-smoke.ps1:195](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/scripts/real-ide-smoke.ps1#L195)
- **Why:** `Screenshot "$Out\dialog-$class.png"` uses the raw class name. If an unknown window class has invalid path characters (such as `:`, `/`, or `\`), `Bitmap.Save` throws `System.ArgumentException`, terminating the script under `$ErrorActionPreference = 'Stop'`.
- **Concrete Fix:** Sanitize the class name before generating the file path:
  ```powershell
  $safeClass = $class -replace '[^\w\.-]', '_'
  Screenshot "$Out\dialog-$safeClass.png"
  ```

---

### [MINOR] Workflow cache key pinned to unversioned rolling Instant Client URL
- **File:Line:** [.github/workflows/real-ide.yml:51, 55](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/.github/workflows/real-ide.yml#L51)
- **Why:** The cache key `oracle-instantclient-basiclite-windows-x64` is static, but the URL `https://download.oracle.com/otn_software/nt/instantclient/instantclient-basiclite-windows.zip` is a moving "latest" pointer updated by Oracle. Once cached, GitHub Actions never invalidates it; a cache miss on another branch will download a newer version, causing non-reproducible CI runs.
- **Concrete Fix:** Pin the Instant Client URL to a specific version and include the version in the cache key:
  ```yaml
  env:
    OCI_VERSION: 23.5.0.24.07
    OCI_URL: https://download.oracle.com/otn_software/nt/instantclient/2350000/instantclient-basiclite-windows.x64-23.5.0.24.07.zip
  ...
  key: oracle-instantclient-basiclite-windows-x64-${{ env.OCI_VERSION }}
  ```

---

### [MINOR] Moving trial installer URL risks 404 failure when vendor publishes new releases
- **File:Line:** [.github/workflows/real-ide.yml:23-24](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/.github/workflows/real-ide.yml#L23-L24)
- **Why:** Allround Automations removes or replaces previous trial MSIs on `/files/trial/` when minor maintenance versions (e.g. `17.0.4`) are published, causing `curl -fsSL` to fail with HTTP 404.
- **Concrete Fix:** Parameterize `PLSQLDEV_MSI` and `PLSQLDEV_URL` as workflow inputs or repository variables, and verify HTTP status before downloading.
- **Licensing Reminder:** The trial MSI is proprietary software subject to Allround Automations' EULA forbidding public redistribution. The MSI must never be committed to Git or published as a GitHub release asset.

---

### [MINOR] Selection replacement is omitted from real IDE verification
- **File:Line:** [SelfTest.cs:407-428](file:///C:/Users/kivil/AppData/Local/Temp/claude/C--Users-kivil-MyFiles-MyProjects-ai-sport-bets/b6b3646f-f094-4309-b899-b9d1f98d17b4/scratchpad/tt-review2/src/TextTools.Plugin/SelfTest.cs#L407-L428)
- **Why:** The test creates a SQL window with no selection (`selected.Length == 0`). Every command is executed against the entire window text. The core value of TextTools—locating and replacing active editor selections via `SelectionLocator.Replace`—is never executed in the real IDE.
- **Concrete Fix:** Add a test verifying `SelectionLocator` behavior in a real window (e.g., set text, select a range or position cursor, and verify replacement preserves outer text).

---

## 2. Recommendations for Next Steps

### A. Extending to a Version Matrix (17 / 16 / 15 / 14 / 13)
1. **Architecture constraint:** TextTools is x64 Native AOT only. PL/SQL Developer 15, 14, and 13 ship in both 32-bit and 64-bit editions. The matrix MUST download and install `*x64.msi` (e.g. `plsqldev1602x64.msi`, `plsqldev1504x64.msi`, `plsqldev1406x64.msi`). A 32-bit IDE will fail to load the x64 DLL.
2. **First-start dialog differences:**
   - v17/v16 have `TClientWizardForm`. Older versions (v14/v13) do not; they go straight to `TLogOnForm`.
   - Registration dialogs vary across versions: button captions may be `Trial`, `Evaluate`, or `Cancel`. Update `real-ide-smoke.ps1` to try alternative button captions (`@('Trial', 'Evaluate', 'OK')`).
3. **Workflow matrix structure:**
   ```yaml
   strategy:
     fail-fast: false
     matrix:
       include:
         - version: '17'
           msi: plsqldev1703x64.msi
           url: https://www.allroundautomations.com/files/trial/plsqldev1703x64.msi
         - version: '16'
           msi: plsqldev1602x64.msi
           url: https://www.allroundautomations.com/files/trial/plsqldev1602x64.msi
   ```

### B. Extending to a DB-Connected Test Safely (Oracle Autonomous DB)
1. **Credential injection:**
   - Store the Oracle Cloud Wallet as base64 in `secrets.OCI_WALLET_BASE64`.
   - Store credentials in `secrets.OCI_DB_USER` and `secrets.OCI_DB_PASSWORD`.
   - Extract the wallet to `C:\oracle\network\admin` and set `TNS_ADMIN=C:\oracle\network\admin`.
2. **Do NOT pass credentials on the command line:**
   - Invoking `plsqldev.exe userid=user/pass@db` exposes passwords in process lists (`Get-CimInstance Win32_Process`) and crash dumps.
   - Use Oracle Instant Client's secure external password store (`cwallet.sso` with autologin alias: `/@alias`).
3. **Prevent credential leakage in screenshots:**
   - `real-ide-smoke.ps1` captures full-screen screenshots uploaded to artifacts. PL/SQL Developer displays the user and database name in its title bar (`user@db - PL/SQL Developer`), and connection dialogs expose connection parameters.
   - Either disable screenshots on DB-connected runs, redact window headers, or restrict artifact uploads to internal non-fork runs.
4. **Least-privilege test account:**
   - The test user should only possess `CREATE SESSION` and `SELECT` privileges on mock package definitions. Never connect as `ADMIN`.

---

## 3. What Was Checked and Found Correct

1. **Unmanaged Calling Conventions:**
   - `AfterStart` and `CommandLine` correctly specify `[UnmanagedCallersOnly(EntryPoint = ..., CallConvs = [typeof(CallConvCdecl)])]`, matching PL/SQL Developer's C-export expectations.
2. **Callback Indices & Signatures:**
   - Callback 20 (`IDE_CreateWindow(WindowType, Text, Execute)`: `unmanaged[Cdecl]<int, byte*, int, void>`) matches `PlugInIntf.pas`.
   - Callback 180 (`IDE_CommandFeedback(FeedbackHandle, S)`: `unmanaged[Cdecl]<int, byte*, void>`) matches `PlugInIntf.pas`.
3. **Native Memory Management in `Ide.cs`:**
   - `Ide.CreateWindow` and `Ide.CommandFeedback` allocate UTF-8 strings via `Marshal.StringToCoTaskMemUTF8` and guarantee deallocation in `finally` blocks via `WithNative`.
4. **Passive Safety for Normal Users:**
   - When `TEXTTOOLS_SELFTEST` is not set, `StartIfRequested()` returns immediately without allocating resources or arming timers.
5. **Exception Guarding in `Exports.cs`:**
   - Both `AfterStart` and `CommandLine` are wrapped in `Guard(...)`, catching managed exceptions and preventing them from escaping the Native AOT unmanaged boundary.
6. **JSON Serialization:**
   - `SelfTest.Write` generates valid UTF-8 JSON without BOM, properly escaping quotes, backslashes, and ASCII control characters (`\u00xx`).
7. **Native AOT Pinning:**
   - The module correctly pins itself in memory on initialization via `GetModuleHandleEx PIN`, ensuring the Native AOT runtime threads remain valid.
8. **Host Emulator Verification:**
   - The emulator pump loop in `Scenarios.SelfTest` correctly exercises both `AfterStart` timer dispatch via `PeekMessageW` / `DispatchMessageW` and `CommandLine` feedback.
