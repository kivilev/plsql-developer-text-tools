using System.Runtime.InteropServices;

namespace TextTools.Plugin;

/// <summary>
/// Strings returned to PL/SQL Developer must stay valid after the export returns.
/// They are few and fixed (menu captions, names), so we allocate each once and never free it.
/// </summary>
internal static unsafe class NativeStrings
{
    private static readonly Dictionary<string, nint> Cache = new(StringComparer.Ordinal);

    public static byte* Get(string s)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(s, out var p))
            {
                p = Marshal.StringToCoTaskMemUTF8(s);
                Cache[s] = p;
            }
            return (byte*)p;
        }
    }
}
