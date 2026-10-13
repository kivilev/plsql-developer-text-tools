# Security policy

Text Tools runs inside the PL/SQL Developer process, with access to the editor text and the IDE's database session
(it only reads `SYSTIMESTAMP`). Reports about anything that could crash the IDE, corrupt text, leak data or run code
are welcome.

## Supported versions

Only the [latest release](https://github.com/kivilev/plsql-developer-text-tools/releases/latest) gets fixes.

## Reporting a vulnerability

Please do **not** open a public issue. Use
[GitHub private vulnerability reporting](https://github.com/kivilev/plsql-developer-text-tools/security/advisories/new).

You will get an answer within 7 days. A confirmed problem is fixed in a patch release and credited in the advisory
unless you prefer otherwise.

## Verifying downloads

Every release lists SHA-256 checksums (`SHA256SUMS.txt`) and carries a build provenance attestation:
`gh attestation verify TextTools.dll -R kivilev/plsql-developer-text-tools`.
