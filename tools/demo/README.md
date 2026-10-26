# Demo animation

`docs/assets/demo.gif` (the README demo) is rendered from `demo.html`, a mock-up of a PL/SQL Developer SQL window.
Every text the plug-in produces in it is real output of `TextTools.Core`: when a command caption or output changes,
update `SCENES` / the menus in `demo.html` and render again.

```powershell
node tools/demo/render.mjs --frames 2.6,4.5,28    # PNG stills in tools/demo/out/ for review
$env:FFMPEG = 'C:\path\to\ffmpeg.exe'             # if ffmpeg is not on PATH
node tools/demo/render.mjs                        # docs/assets/demo.gif, 12 fps
node tools/demo/render.mjs --social               # docs/assets/social-preview.png (Settings > Social preview)
```

Needs Node 22+, Chrome or Edge, ffmpeg. Open `demo.html` in a browser to watch it in real time.

## Recording the real IDE (to replace the mock-up)

[ScreenToGif](https://www.screentogif.com/), PL/SQL Developer window 1280×720 or smaller, a light theme, a larger editor font
(14–15 pt), connected to a database. Same five scenes, about 7 s each, pause 1-2 s on each result:

1. A procedure as the Beautifier leaves it -> Plug-Ins -> Format Tools -> **Apply formatting rules**.
2. The `select ... where status = c_new` line inside a block with `c_new constant` -> SQL Tools -> **Copy SQL with constants resolved** (the status bar says "SQL copied: 1 constant"; paste into a new SQL Window to show it).
3. Lines `1042` ... `4211` under `where customer_id` -> select them -> String Tools -> **List -> IN (...)**.
4. `select customerId, firstName, orderDate` -> select the identifiers -> String Tools -> **snake_case**.
5. Select `13.10.2026 14:30` -> Date Tools -> **Date -> TO_DATE(...)**.

Export at 10–12 fps, crop to the window, keep it under ~3 MB, save as `docs/assets/demo.gif`.
