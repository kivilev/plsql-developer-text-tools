using System.Runtime.InteropServices;
using System.Text;

namespace TextTools.Plugin;

/// <summary>Export and import of the rules as a file (the file dialogs of the system, the text in the style of the Beautifier's .br file).</summary>
internal static unsafe partial class SettingsWindow
{
    private const int FileNameChars = 1024;

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public uint StructSize;
        public nint Owner;
        public nint Instance;
        public nint Filter;
        public nint CustomFilter;
        public uint MaxCustomFilter;
        public uint FilterIndex;
        public nint File;
        public uint MaxFile;
        public nint FileTitle;
        public uint MaxFileTitle;
        public nint InitialDir;
        public nint Title;
        public uint Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        public nint DefaultExtension;
        public nint CustomData;
        public nint Hook;
        public nint TemplateName;
        public nint Reserved;
        public uint ReservedDword;
        public uint FlagsEx;
    }

    [LibraryImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW")]
    private static partial int GetSaveFileName(ref OpenFileName dialog);

    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    private static partial int GetOpenFileName(ref OpenFileName dialog);

    /// <summary>Asks for a file name; null when the user cancels.</summary>
    private static string? AskFile(bool save, string suggestedName)
    {
        var filter = _texts.Text("dialog.filter") + " (*.ttr;*.br)\0*.ttr;*.br\0*.*\0*.*\0\0";
        var extension = "ttr\0";
        var name = stackalloc char[FileNameChars];
        new Span<char>(name, FileNameChars).Clear();
        suggestedName.AsSpan(0, Math.Min(suggestedName.Length, FileNameChars - 1)).CopyTo(new Span<char>(name, FileNameChars));
        fixed (char* filterPointer = filter)
        fixed (char* extensionPointer = extension)
        {
            var dialog = new OpenFileName
            {
                StructSize = (uint)sizeof(OpenFileName),
                Owner = _hwnd,
                Filter = (nint)filterPointer,
                FilterIndex = 1,
                File = (nint)name,
                MaxFile = FileNameChars,
                DefaultExtension = (nint)extensionPointer,
                // OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | OFN_EXPLORER, and OFN_OVERWRITEPROMPT or OFN_FILEMUSTEXIST
                Flags = 0x800 | 0x8 | 0x80000 | (save ? 0x2u : 0x1000u),
            };
            var ok = save ? GetSaveFileName(ref dialog) : GetOpenFileName(ref dialog);
            return ok != 0 ? new string(name) : null;
        }
    }

    private static void ExportRules()
    {
        ReadControlsIntoProfile();
        var path = AskFile(save: true, "format-rules.ttr");
        if (path is null) return;
        try
        {
            File.WriteAllText(path, _profile.ToRulesText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Ide.SetStatusMessage(_texts.Text("settings.exported"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Ide.SetStatusMessage(_texts.Text("settings.fileError"));
        }
    }

    private static void ImportRules()
    {
        var path = AskFile(save: false, "");
        if (path is null) return;
        try
        {
            ReadControlsIntoProfile();
            var applied = _profile.ReadRulesText(File.ReadAllText(path, Encoding.UTF8));
            PushProfileToControls();
            UpdatePreview();
            Ide.SetStatusMessage(_texts.Text("settings.imported") + applied.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Ide.SetStatusMessage(_texts.Text("settings.fileError"));
        }
    }
}
