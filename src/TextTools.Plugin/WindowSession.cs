using TextTools.Core;

namespace TextTools.Plugin;

/// <summary>
/// The database queries of one command run as the user of the current window, like the IDE's own F5: a window can keep
/// its connection after the main one was switched. When the two differ the plug-in uses a private session of the
/// window's connection until disposed; otherwise nothing changes and the queries stay in the main session.
/// </summary>
internal sealed class WindowSession : IDisposable
{
    private readonly bool _private;

    private WindowSession(int connection, bool isPrivate, bool failed) => (Connection, _private, Failed) = (connection, isPrivate, failed);

    /// <summary>The IDE's index of the window's connection, -1 for the main connection.</summary>
    public int Connection { get; }

    /// <summary>The session could not be opened: no queries, they would run as the wrong user.</summary>
    public bool Failed { get; }

    public static string FailureText => UiTexts.For(Ide.TranslationLanguage, Ide.TranslationFile).Text("session.failed");

    public static WindowSession Open()
    {
        var connection = Ide.OtherWindowConnection();
        if (connection < 0) return new WindowSession(-1, isPrivate: false, failed: false);
        var opened = Ide.UseConnectionSession(connection);
        if (!opened) Ide.UseDefaultSession(); // the details may have been accepted before the session failed
        return new WindowSession(connection, opened, failed: !opened);
    }

    public void Dispose()
    {
        if (_private) Ide.UseDefaultSession();
    }
}
