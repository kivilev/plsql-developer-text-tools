---
name: release
description: Publishes a version of Text Tools — version bump, tag, GitHub release with the zipped TextTools.dll and its SHA-256, release notes from squash commits, and the submission to the Allround Automations plug-ins page. Use when the user wants to release, tag a version, publish the DLL, or submit the plugin to Allround.
---

# Release

1. **Version**: `Version` in `Directory.Build.props` (SemVer; it shows in About). Bump in an `opt` PR
   (`opt bump version to 0.2.0`), merge.
2. **Gates**: on fresh master `scripts/verify.ps1` green, plus the manual list of skill `install-to-ide` in the real IDE.
3. **Artifact**:
   ```powershell
   dotnet publish src/TextTools.Plugin -c Release
   $v = '0.2.0'
   Compress-Archive src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/TextTools.dll "artifacts/TextTools-$v-x64.zip" -Force
   (Get-FileHash "artifacts/TextTools-$v-x64.zip" -Algorithm SHA256).Hash
   ```
   Ship only `TextTools.dll` (no .pdb).
4. **Tag and release** (the user confirms before publishing):
   ```powershell
   git tag -a "v$v" -m "v$v"; git push origin "v$v"
   gh release create "v$v" "artifacts/TextTools-$v-x64.zip" --title "v$v" --notes-file notes.md
   ```
   Notes: group squash commits since the previous tag by `feat` / `fix` / `opt` (`git log v0.1.0..HEAD --oneline`),
   add the SHA-256, install steps (copy the DLL into `PlugIns`, restart, Configure > Plug-Ins), requirements
   (PL/SQL Developer x64, 12+; tested on 14).
5. **Allround plug-ins page** (first public release only): the repo must be public with a license chosen;
   email the vendor (address on https://www.allroundautomations.com/products/pl-sql-developer/plug-ins/ "Enlist your
   own plug-in") with name, one-line description, download link, requirements, author. Draft the email for the user;
   sending is the user's action.
