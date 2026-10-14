namespace TextTools.Plugin;

/// <summary>
/// Ribbon button images. The IDE loads them from a file (a PNG keeps its transparency), so the PNGs embedded in the DLL
/// are written to %LOCALAPPDATA%\TextTools\icons once and passed by path. Any failure means a button without an image.
/// </summary>
internal static class RibbonIcons
{
    public static string? FileFor(string menu)
    {
        try
        {
            var name = menu.Replace(' ', '-').ToLowerInvariant() + ".png";
            using var stream = typeof(RibbonIcons).Assembly.GetManifestResourceStream("TextTools.Icons." + name);
            if (stream is null) return null;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var png = buffer.ToArray();

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextTools", "icons");
            var file = Path.Combine(dir, name);
            if (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual(png))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(file, png);
            }
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
