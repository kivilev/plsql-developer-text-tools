using TextTools.Core;

namespace TextTools.Plugin;

/// <summary>
/// Maps CreateMenuItem indices (1..99) to captions and OnMenuClick indices back to commands.
/// Version 12+ uses the ribbon layout (GROUP / MENUITEM / SUBITEM), older versions a "Tools / ..." path.
/// </summary>
internal static class Menu
{
    /// <summary>Short name for the ribbon group and status bar.</summary>
    public const string Title = "Text Tools";

    /// <summary>Full product name for the Plug-Ins configuration dialog, About and publications.</summary>
    public const string ProductName = "Text Tools for PL/SQL Developer";

    private static readonly Dictionary<int, string> Captions = [];
    private static readonly Dictionary<int, Command> ByIndex = [];
    private static readonly Dictionary<int, string> MenuButtons = [];

    /// <summary>Ribbon dropdown buttons (MENUITEM): index → menu name, for their images.</summary>
    public static IReadOnlyDictionary<int, string> Buttons => MenuButtons;

    public static void Build(int sysVersion)
    {
        Captions.Clear();
        ByIndex.Clear();
        MenuButtons.Clear();
        // One submenu per Command.Menu ("String Tools", "Date Tools"; dropdown buttons on the ribbon),
        // a flat list inside with groups separated by "-".
        var ribbon = sysVersion >= 1200;
        var index = 1;
        if (ribbon) Captions[index++] = $"GROUP={Title}";

        foreach (var menu in Commands.All.GroupBy(c => c.Menu, StringComparer.Ordinal))
        {
            if (ribbon)
            {
                MenuButtons[index] = menu.Key;
                Captions[index++] = $"MENUITEM={menu.Key}";
            }
            var first = true;
            foreach (var group in menu.GroupBy(c => c.Group, StringComparer.Ordinal))
            {
                if (!first) Captions[index++] = ribbon ? "SUBITEM=-" : $"Tools / {Title} / {menu.Key} / -";
                first = false;
                foreach (var cmd in group)
                {
                    if (index > 99) return;
                    Captions[index] = ribbon ? $"SUBITEM={cmd.Name}" : $"Tools / {Title} / {menu.Key} / {cmd.Name}";
                    ByIndex[index++] = cmd;
                }
            }
        }
    }

    public static string Caption(int index) => Captions.GetValueOrDefault(index, "");

    public static Command? Find(int index) => ByIndex.GetValueOrDefault(index);

    /// <summary>The menu index of the command with this name, or null.</summary>
    public static int? IndexOf(string name) =>
        ByIndex.Where(p => string.Equals(p.Value.Name, name, StringComparison.Ordinal)).Select(p => (int?)p.Key).FirstOrDefault();
}
