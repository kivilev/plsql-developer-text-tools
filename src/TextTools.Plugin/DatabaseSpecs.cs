using System.Diagnostics;
using TextTools.Core.Sql;

namespace TextTools.Plugin;

/// <summary>
/// Package specifications from the database of the current window: read-only dictionary queries in the session the
/// caller opened (<see cref="WindowSession"/>), only while the window is not executing (the query would wait for the user's statement).
/// Lookups are plain indexed ones (see <see cref="SpecQuery"/>): the window's own package by the owner the IDE knows,
/// another package of the user (USER_SOURCE), then through a synonym. Results (also "no such package") are cached for
/// a few seconds only: the developer edits and recompiles packages all the time.
/// </summary>
internal sealed class DatabaseSpecs : ISpecSource
{
    /// <summary>Enough for any real package specification; the rest of a monster one is not read.</summary>
    private const int MaxLines = 20_000;

    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(5);
    private static readonly Dictionary<string, (DateTime At, string? Spec)> Cache = new(StringComparer.Ordinal);

    private readonly (string Owner, string Name)? _window;
    private readonly int _connection;
    private readonly Stopwatch _database = new();

    private DatabaseSpecs((string Owner, string Name)? window, int connection) => (_window, _connection) = (window, connection);

    /// <summary>Time spent in dictionary queries during this copy.</summary>
    public TimeSpan DatabaseTime => _database.Elapsed;

    /// <summary>A source when the IDE is connected and idle, otherwise null (constants from the editor only).</summary>
    public static DatabaseSpecs? IfConnected(WindowSession session)
    {
        if (session.Failed || !Ide.Connected || Ide.WindowIsRunning) return null;
        var window = Ide.GetWindowObject();
        return new DatabaseSpecs(window is { } w ? (w.Owner, w.Name) : null, session.Connection);
    }

    public string? PackageSpec(string? owner, string package)
    {
        // The window's own object (a package body in a Program Window): its owner is known, no search needed.
        if (owner is null && _window is { } w && string.Equals(w.Name, package, StringComparison.OrdinalIgnoreCase))
        {
            owner = w.Owner;
        }
        var key = $"{_connection}:{owner}.{package}"; // another user may see another package of this name, or none
        if (Cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < CacheTime) return hit.Spec;

        var spec = Load(owner, package);
        Cache[key] = (DateTime.UtcNow, spec);
        return spec;
    }

    private string? Load(string? owner, string package)
    {
        _database.Start();
        try
        {
            var spec = Lines(SpecQuery.Source(owner, package));
            if (spec is not null || owner is not null) return spec;
            return SpecQuery.ParseTarget(Ide.QueryColumn(SpecQuery.Synonym(package), 1)?.FirstOrDefault()) is { } target
                ? Lines(SpecQuery.Source(target.Owner, target.Name))
                : null;
        }
        catch
        {
            return null; // no dictionary access, lost connection: resolve without the database
        }
        finally
        {
            _database.Stop();
        }
    }

    private static string? Lines(string sql) => Ide.QueryColumn(sql, MaxLines) is { Count: > 0 } lines ? string.Concat(lines) : null;
}
