using System.Runtime.InteropServices;

namespace TextTools.HostEmulator;

/// <summary>Calls the plugin exports in the order PL/SQL Developer does and keeps the menu it built.</summary>
internal sealed unsafe class PluginHost
{
    private readonly delegate* unmanaged[Cdecl]<int, void> _onMenuClick;

    public PluginHost(nint library, bool registerCallbacks)
    {
        Library = library;
        var register = (delegate* unmanaged[Cdecl]<int, nint, void>)NativeLibrary.GetExport(library, "RegisterCallback");
        var identify = (delegate* unmanaged[Cdecl]<int, byte*>)NativeLibrary.GetExport(library, "IdentifyPlugIn");
        var createMenu = (delegate* unmanaged[Cdecl]<int, byte*>)NativeLibrary.GetExport(library, "CreateMenuItem");
        var onActivate = (delegate* unmanaged[Cdecl]<void>)NativeLibrary.GetExport(library, "OnActivate");
        var about = (delegate* unmanaged[Cdecl]<byte*>)NativeLibrary.GetExport(library, "About");
        _onMenuClick = (delegate* unmanaged[Cdecl]<int, void>)NativeLibrary.GetExport(library, "OnMenuClick");

        if (registerCallbacks)
        {
            FakeIde.Register(register);
        }
        register(-1, 0);        // garbage indices must be ignored
        register(100_000, 0);

        Identity = Marshal.PtrToStringUTF8((nint)identify(7)) ?? "";
        for (var i = 0; i <= 120; i++)   // the IDE asks 1..99; indices outside must be harmless
        {
            var caption = Marshal.PtrToStringUTF8((nint)createMenu(i));
            if (!string.IsNullOrEmpty(caption))
            {
                Menu[caption] = i;
            }
        }
        onActivate();
        AboutText = Marshal.PtrToStringUTF8((nint)about()) ?? "";
    }

    public nint Library { get; }
    public string Identity { get; }
    public string AboutText { get; }
    public Dictionary<string, int> Menu { get; } = new(StringComparer.Ordinal);

    public IReadOnlyList<int> GetCommandIndices() =>
        Menu.Where(kv => kv.Key.StartsWith("SUBITEM=", StringComparison.Ordinal) && !string.Equals(kv.Key, "SUBITEM=-", StringComparison.Ordinal))
            .Select(kv => kv.Value).ToList();

    public void Click(int index) => _onMenuClick(index);

    /// <summary>Puts text, selection and cursor into the fake editor, clicks the item, returns the new text.</summary>
    public string Click(string item, string text, string selected = "", int x = 1, int y = 1)
    {
        (FakeIde.Text, FakeIde.Selected, FakeIde.X, FakeIde.Y) = (text, selected, x, y);
        _onMenuClick(Menu["SUBITEM=" + item]);
        return FakeIde.Text;
    }
}
