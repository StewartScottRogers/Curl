---
id: BL-3889
title: Fix CGR-043 part 3: --proto-default smb is rejected as unsupported protocol (exit 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3888]
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043SmbProtoDefaultTests.cs, Curl.Cli.UnitTests/CommandLineProtocolSetOptionTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1627SchemeLookup.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3889 — Fix CGR-043 part 3: --proto-default smb is rejected as unsupported protocol (exit 1)

## Goal

On Windows `curl --proto-default smb -sS 127.0.0.1:20002/share/x` behaves as curl 8.22.0 Schannel does: stderr `a specified protocol is unsupported by libcurl`, exit 1.

## Context

Part 3 of 3 split from BL-3572 (CGR-043 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Today Curl accepts `--proto-default smb` and tries to connect (exit 7). Start at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`, which parts 1 and 2 also change, so it runs after them. Measure real curl before pinning text (the report's wording may lack the `curl: (1)` prefix).

## Acceptance criteria

- [x] `curl --proto-default smb -sS 127.0.0.1:20002/share/x` through real curl and through Curl gives the same exit code (1), stdout bytes, stderr text and no request bytes on the wire; commands and both results are recorded in Notes.
- [x] A focused unit test in `Curl.Cli.UnitTests/Cgr043SmbProtoDefaultTests.cs` fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in CGR-043 that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3572, which went over its cost cap.
- Fix: `CommandLineProtocolSet.KnownScheme` (read by `--proto-default`) now looks in the `--proto` set, which leaves out `smb` and `smbs`, so `--proto-default smb` is refused as unsupported (exit 1) before any connection. A new `SchemeLibcurlNames` keeps the full table, `smb` and `smbs` included, for the unit1627 port (`Curl_get_scheme` knows them), so `Unit1627SchemeLookup.cs` now calls it. No new ADR: the behaviour follows ADR-0835 (BL-3888), which already decided that neither reference build has SMB.
- Touches widened (2026-10-28) by `Curl.Cli.UnitTests/CommandLineProtocolSetOptionTests.cs`, whose `--proto-default SMBS` row expected the old acceptance (now `DICT`), and `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1627SchemeLookup.cs`, which used `KnownScheme` for the libcurl table. No task in Doing on origin/work/dark-factory names either file. `CommandLineOptionTable.cs` (held by BL-3862) was left alone.
- Measured 2026-10-28 with `Record-CurlExchange.ps1 -Port 20002 -CurlArgs '--proto-default','smb','-sS','127.0.0.1:20002/share/x'` against the reference curl and with `-Curl Curl.Console\bin\Debug\net10.0\curl.exe`: both exit 1, write 0 stdout bytes and 0 request bytes, and write the same 143 stderr bytes: `curl: option --proto-default: a specified protocol is unsupported by libcurl` and `curl: try 'curl --help' or 'curl --manual' for more information`.
- The other CGR-043 variants still match, both binaries: `-sS smb://127.0.0.1:20002/share/x` gives exit 1, `curl: (1) Protocol "smb" is disabled`; `--proto =smb -sS smb://...` gives exit 2, the unrecognized-protocol line, the badly-used line and the try-help line; no request bytes in either case.
- `Cgr043SmbProtoDefaultTests` (3 rows) failed 3/3 with `KnownScheme` put back on the full table, and passes with the fix. Fast tests all green; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Conformance.UnitProgram.UnitLibrary` reported 0 failing members. The change does not depend on the platform (no paths or OS text), so it holds on Linux and macOS too.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 037eea96 50a598e7 4d4927c7 779790c0.
- 2026-10-28: Doing -> Done. --proto-default smb and smbs are refused as unsupported by libcurl with exit 1 and nothing sent, as curl 8.22.0 does
