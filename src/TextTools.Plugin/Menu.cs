using TextTools.Core;
using TextTools.Core.Plan;

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
    private static readonly Dictionary<int, string> Displayed = [];

    /// <summary>Ribbon dropdown buttons (MENUITEM): index → menu name, for their images.</summary>
    public static IReadOnlyDictionary<int, string> Buttons => MenuButtons;

    /// <summary>
    /// Builds the menu in the language of the IDE's translation. The captions travel as UTF-8, so they are translated
    /// only once the IDE reads UTF-8 (CHARMODE); until then, and for English, they are the ASCII English ones.
    /// The English caption stays the command's identity (lookups, icons, tests); only the displayed text changes.
    /// </summary>
    public static void Build(int sysVersion, PlanReference? reference = null)
    {
        reference ??= Ide.Utf8 ? PlanReference.For(Ide.TranslationLanguage, Ide.TranslationFile) : PlanReference.Default;
        Captions.Clear();
        ByIndex.Clear();
        MenuButtons.Clear();
        Displayed.Clear();
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
                Displayed[index] = reference.Menu(menu.Key);
                Captions[index++] = $"MENUITEM={reference.Menu(menu.Key)}";
            }
            var menuName = reference.Menu(menu.Key);
            var first = true;
            foreach (var group in menu.GroupBy(c => c.Group, StringComparer.Ordinal))
            {
                if (!first) Captions[index++] = ribbon ? "SUBITEM=-" : $"Tools / {Title} / {menuName} / -";
                first = false;
                foreach (var cmd in group)
                {
                    if (index > 99) return;
                    var name = reference.Menu(cmd.Name);
                    Displayed[index] = name;
                    Captions[index] = ribbon ? $"SUBITEM={name}" : $"Tools / {Title} / {menuName} / {name}";
                    ByIndex[index++] = cmd;
                }
            }
        }
    }

    public static string Caption(int index) => Captions.GetValueOrDefault(index, "");

    /// <summary>The text of an item or ribbon dropdown as the user sees it (for the context menu and the ribbon buttons).</summary>
    public static string DisplayName(int index) => Displayed.GetValueOrDefault(index, "");

    public static Command? Find(int index) => ByIndex.GetValueOrDefault(index);

    /// <summary>The menu index of the command with this name, or null.</summary>
    public static int? IndexOf(string name) =>
        ByIndex.Where(p => string.Equals(p.Value.Name, name, StringComparison.Ordinal)).Select(p => (int?)p.Key).FirstOrDefault();
}
