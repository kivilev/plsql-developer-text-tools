// Integration test of the native plugin DLL without PL/SQL Developer.
// Loads TextTools.dll the way the IDE does, registers fake IDE callbacks and clicks menu items.
// Usage: HostEmulator <path to TextTools.dll> <mode>; exit code 0 = all checks passed.
// Modes: smoke | stress | nocallbacks | noutf8 | unload | all (each mode runs in its own process).

using System.Diagnostics;
using System.Runtime.InteropServices;
using TextTools.HostEmulator;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: HostEmulator <TextTools.dll> smoke|stress|nocallbacks|noutf8|unload|all");
    return 2;
}

var dll = Path.GetFullPath(args[0]);
var mode = args[1];

if (string.Equals(mode, "all", StringComparison.Ordinal))
{
    var failed = 0;
    foreach (var m in Scenarios.Modes)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (!string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "HostEmulator", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(typeof(Scenarios).Assembly.Location);   // started via "dotnet HostEmulator.dll"
        }
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add(m);
        using var p = Process.Start(start)!;
        p.WaitForExit();
        if (p.ExitCode != 0) failed++;
    }
    Console.WriteLine(failed == 0 ? "ALL MODES PASSED" : $"{failed} MODE(S) FAILED");
    return failed == 0 ? 0 : 1;
}

try
{
    var host = new PluginHost(NativeLibrary.Load(dll), registerCallbacks: !string.Equals(mode, "nocallbacks", StringComparison.Ordinal));
    var ok = Scenarios.Run(mode, host, dll);
    Console.WriteLine($"[{mode}] {(ok ? "PASS" : "FAIL")}");
    return ok ? 0 : 1;
}
catch (Exception e)
{
    Console.WriteLine($"[{mode}] FAIL: {e}");
    return 1;
}
