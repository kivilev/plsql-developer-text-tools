---
name: install-to-ide
description: Installs the freshly built TextTools.dll into the local PL/SQL Developer (Program Files, needs UAC) and walks through the manual check list in the real IDE. Use when the user wants to try a build in PL/SQL Developer, after interop changes, or before a release.
---

# Install into PL/SQL Developer

Local IDE: `C:\Program Files\PLSQL Developer 14` (x64). Plug-ins folder: `PlugIns\`.

1. Build: `dotnet publish src/TextTools.Plugin -c Release` (or `scripts/verify.ps1`).
2. **The IDE must be closed** (it locks loaded DLLs): check `Get-Process plsqldev`. Ask the user to close it; never kill it.
3. Copy with elevation (the user confirms UAC). Remove leftovers of old names (`StringTools.dll`) so there are no duplicates:

```powershell
$src = "$PWD\src\TextTools.Plugin\bin\Release\net10.0\win-x64\publish\TextTools.dll"
$dir = "C:\Program Files\PLSQL Developer 14\PlugIns"
$cmd = "Copy-Item -LiteralPath '$src' -Destination '$dir' -Force; Remove-Item -LiteralPath '$dir\StringTools.dll' -Force -ErrorAction SilentlyContinue"
Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile -Command $cmd"
(Get-FileHash $src).Hash -eq (Get-FileHash "$dir\TextTools.dll").Hash   # must be True
Start-Process "C:\Program Files\PLSQL Developer 14\plsqldev.exe"
```

4. If the menu is missing: Configure > Plug-Ins > enable "Text Tools for PL/SQL Developer" (a changed
   `IdentifyPlugIn` name resets the enabled flag), restart the IDE.

## Manual check list (things only the real IDE can show)

- [ ] Plug-Ins → String Tools / Date Tools submenus, separators between groups, captions not cut.
- [ ] A transform on a selection (e.g. `first_name, last_name` → camelCase) and the status bar example.
- [ ] **Ctrl+Z** restores the text after a command (is `IDE_SetText` undoable?).
- [ ] Selection on a line that starts with a tab (char index vs visual column in `GetCursorX`).
- [ ] Cyrillic in the text survives (`CHARMODE=UTF8`).
- [ ] Date Tools → Now: "from PC clock" without a connection, "from database" when connected.
- [ ] Bad input (e.g. Base64 decode on `!!!`): status message, no dialog.

Record results in `docs/journal.md`; a defect becomes a GitHub issue (skill `fix-bug`).
