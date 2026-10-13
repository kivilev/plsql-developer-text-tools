---
name: plsqldev-plugin-api
description: Reference for the Allround Automations PL/SQL Developer plug-in API as used by this plugin — exported functions, callback indices and signatures, menu caption format, charset mode, SQL calls, and the crash-safety rules for Native AOT. Use whenever code in src/TextTools.Plugin changes, a new IDE callback is needed, the menu layout changes, or something behaves differently in the real IDE than in tests.
---

# PL/SQL Developer plug-in API

Source of truth (installed with the IDE): `C:\Program Files\PLSQL Developer 14\PlugInDoc\plugindoc.pdf`
and `PlugInDoc\Interface\PlugInIntf.pas` (callback indices). Vendor demos: `PlugInDoc\Delphi\Demo2`.
Never guess an index or a signature: read the .pas file.

## Calling convention

- A plug-in is a DLL; the IDE loads every DLL in `<IDE>\PlugIns` that exports `IdentifyPlugIn`.
- cdecl; parameters are only 32-bit Integer, 32-bit Bool (0/1) and zero-terminated single-byte `char*`.
- x86 and x64 need separate builds; we ship x64 only.
- Strings returned by the plug-in must stay valid after the call: `NativeStrings` caches them (never freed — the set
  is small and fixed). Strings returned by the IDE are owned by the IDE: copy them (`Marshal.PtrToStringUTF8`), never free.
- Strings passed to the IDE are allocated per call and freed in `finally` (`Ide.WithNative`).

## Exports we implement (`Exports.cs`)

| Export | When the IDE calls it | Notes |
|---|---|---|
| `IdentifyPlugIn(int id) -> char*` | first | store the id; pin the module; return the product name (shown in Configure > Plug-Ins) |
| `RegisterCallback(int index, void* f)` | for every callback | store in `Ide.Callbacks[index]`; ignore out-of-range indices |
| `CreateMenuItem(int index 1..99) -> char*` | while building menus | `""` = no item |
| `OnActivate()` | after load / when enabled | set `CHARMODE=UTF8` via `IDE_PlugInSetting` (219) |
| `OnMenuClick(int index)` | menu click | dispatch to `Editor.Apply` |
| `PlugInName() -> char*` | | `TextTools`: used for online updates and command-window commands; keep stable |
| `About() -> char*` | About button | |

Other available events (not used yet): `OnCreate`, `OnDeactivate`, `OnDestroy`, `CanClose`, `AfterStart`,
`OnWindowChange`, `OnWindowCreated`, `OnConnectionChange`, `OnPopup`, `BeforeExecuteWindow`, `AfterExecuteWindow`,
`RegisterExport`/`ExportInit`/... (custom grid export formats), `CommandLine`.

## Callbacks we use (`Ide.cs`)

| # | Name | Signature |
|---|---|---|
| 1 | SYS_Version | `int()` e.g. 1400 = v14 |
| 10 | IDE_MenuState | `void(int id, int index, Bool enabled)` |
| 11 | IDE_Connected | `Bool()` |
| 15 | IDE_GetAppHandle | `int()` (HWND, parent for message boxes) |
| 26 | IDE_GetReadOnly | `Bool()` |
| 30 | IDE_GetText | `char*()` whole text of the current window |
| 31 | IDE_GetSelectedText | `char*()` |
| 34 | IDE_SetText | `Bool(char*)` **replaces the whole text** |
| 35 | IDE_SetStatusMessage | `Bool(char*)` |
| 40/42/44 | SQL_Execute / SQL_Eof / SQL_Field | `int(char*)` 0 = ok, else ORA code / `Bool()` / `char*(int field, 0-based)`; main session by default |
| 84 | IDE_WindowIsRunning | `Bool()` |
| 141/142/143 | IDE_GetCursorX / Y / SetCursor | 1-based char / line |
| 153 | IDE_WindowHasEditor | `Bool(Bool codeEditor)` |
| 219 | IDE_PlugInSetting | `Bool(int id, char* setting, char* value)`; `CHARMODE` = ANSI\|UTF8\|UTF8BOM |

Also useful: 32 `IDE_GetCursorWord`, 38 `IDE_GetCursorWordPosition`, 120 `IDE_KeyPress`, 150 `IDE_CreateToolButton`,
190-193 result grid access, 162 `IDE_BeautifyText`.

## Selection replacement

There is no "replace selection" call. `SelectionLocator` finds the selection at the cursor (the cursor is at one
end of it), falls back to a unique occurrence, otherwise refuses with a status message. Then `IDE_SetText` + `IDE_SetCursor`.
Open questions to verify in the real IDE: is `IDE_SetText` undoable (Ctrl+Z)? Is `GetCursorX` a char index or a
visual column with tabs?

## Menu captions

- v12+ (`SYS_Version >= 1200`): `GROUP=Text Tools`, then per submenu `MENUITEM=String Tools`, items `SUBITEM=...`,
  separator `SUBITEM=-`. In the classic menu GROUP renders as a separator and MENUITEM as a submenu of Plug-Ins.
- Before v12: paths `Tools / Text Tools / String Tools / item`.
- ASCII only (captions can be read before `CHARMODE=UTF8`), no `[...]` (parsed as options like `[tabindex=3]` and cut out).
  Tests `CaptionsAreAscii`, `CaptionsHaveNoSquareBrackets` enforce it.

## Crash safety (non-negotiable)

1. No exception may leave an export: an unhandled exception in `UnmanagedCallersOnly` terminates the IDE.
   `Guard` + try/catch everywhere; fallback strings are u8 literals (`StaticString`), so the error path cannot fail.
2. Pin the module in `IdentifyPlugIn`: a Native AOT runtime cannot be unloaded; after `FreeLibrary` its GC/finalizer
   threads would run unmapped code. Emulator mode `unload` checks it.
3. Core throws only `FormatException` for bad input → status bar. Overflow / out-of-range → converted in `DateText.PerLine`.
   `RegexMatchTimeoutException` → status bar. Anything else → message box (still no crash).
4. Do not rewrite text containing U+FFFD, or non-ASCII text when UTF-8 mode was not confirmed.
5. SQL in the main session only when `!IDE_WindowIsRunning`; failures fall back to the PC clock.
6. Native AOT strips framework exception texts (`UseSystemResourceKeys`): write our own messages.
