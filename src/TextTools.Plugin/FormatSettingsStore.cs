using System.Text;
using TextTools.Core.Formatting;

namespace TextTools.Plugin;

/// <summary>
/// The saved profile of the formatting rules: a JSON file in the user's application data (<c>%APPDATA%\TextTools\format-rules.json</c>).
/// A file, not the IDE's preferences, so that a team can put the same file into its repository and copy it to each machine.
/// Never throws: a missing, unreadable or broken file means the default profile.
/// </summary>
internal static class FormatSettingsStore
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextTools", "format-rules.json");

    public static FormatProfile Load()
    {
        try
        {
            return File.Exists(Path) ? FormatProfile.FromJson(File.ReadAllText(Path, Encoding.UTF8)) : new FormatProfile();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new FormatProfile();
        }
    }

    /// <summary>False when the file could not be written.</summary>
    public static bool Save(FormatProfile profile)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, profile.ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Makes the saved profile the one the menu command uses.</summary>
    public static void Apply(FormatProfile profile) => PostFormatter.Current = profile.ToOptions();
}
