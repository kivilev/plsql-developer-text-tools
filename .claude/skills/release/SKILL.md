---
name: release
description: Publishes a version of Text Tools — release PR (version + CHANGELOG), tag, the release workflow that builds PlsqlDevTextTools.dll, zip and checksums and drafts the GitHub release, manual checks, publishing, and the submission to the Allround Automations plug-ins page. Use when the user wants to release, tag a version, publish the DLL, or submit the plugin to Allround.
---

# Release

Single source of the version: `<Version>` in `Directory.Build.props` (shown in About). SemVer: PATCH fixes,
MINOR new commands, MAJOR removed/renamed commands (menu captions are public API — users bind hotkeys) or a dropped IDE version.

1. **Release PR** `opt release X.Y.Z`:
   - set `<Version>X.Y.Z</Version>`;
   - draft notes: `sh scripts/release-notes.sh <previous tag or -> X.Y.Z`, paste at the top of `CHANGELOG.md`
     as `## [vX.Y.Z] - YYYY-MM-DD`, rewrite into plain words for users, add `### Breaking` if any;
   - merge after CI is green.
2. **Manual check** in the real IDE on fresh master: skill `install-to-ide` checklist.
3. **Tag** (the user confirms): `git switch master && git pull && git tag -a vX.Y.Z -m vX.Y.Z && git push origin vX.Y.Z`.
   Rehearsal: `vX.Y.Z-rc.N` (marked as pre-release; delete the draft and the tag afterwards:
   `gh release delete vX.Y.Z-rc.N --yes --cleanup-tag`).
4. **Workflow** `.github/workflows/release.yml` (watch: `gh run watch`): checks the tag against `<Version>`, runs
   `scripts/verify.ps1`, `scripts/package.ps1` (dist/: `PlsqlDevTextTools.dll`, `PlsqlDevTextTools-X.Y.Z-win-x64.zip` with README,
   LICENSE, THIRD-PARTY-NOTICES, `SHA256SUMS.txt`), notes from `scripts/changelog-section.ps1`, build provenance
   (public repo only), and creates a **draft** release.
5. **Before publishing the draft**: download the assets, compare `Get-FileHash` with `SHA256SUMS.txt`,
   `gh attestation verify PlsqlDevTextTools.dll -R kivilev/plsql-developer-text-tools` (public repo), upload `PlsqlDevTextTools.dll`
   to VirusTotal (false positives: submit to the AV vendor / Microsoft). Then the user presses Publish.
6. **Allround plug-ins page** (first public release only): draft an email for the user (address under "Enlist your own
   plug-in" on https://www.allroundautomations.com/products/pl-sql-developer/plug-ins/): name, one-line description,
   requirements (x64, 12+, tested versions), license MIT, download link (releases/latest), source link, author.
   Sending is the user's action.

Local packaging without a tag (e.g. to hand a build to a tester):
`dotnet publish src/TextTools.Plugin -c Release; powershell -File scripts/package.ps1 -Version X.Y.Z-dev`.
