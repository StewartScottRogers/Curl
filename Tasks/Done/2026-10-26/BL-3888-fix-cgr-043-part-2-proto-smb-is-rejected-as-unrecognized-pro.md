---
id: BL-3888
title: Fix CGR-043 part 2: --proto =smb is rejected as unrecognized protocol (exit 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3887]
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043SmbProtoTests.cs, Curl.Cli.UnitTests/CommandLineProtocolSetOptionTests.cs, Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3888 — Fix CGR-043 part 2: --proto =smb is rejected as unrecognized protocol (exit 2)

## Goal

On Windows `curl --proto =smb -sS smb://127.0.0.1:20002/share/x` behaves as curl 8.22.0 Schannel does: stderr `curl: unrecognized protocol 'smb'` and `option --proto: is badly used here`, exit 2.

## Context

Part 2 of 3 split from BL-3572 (CGR-043 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Today Curl accepts `--proto =smb` and tries to connect (exit 7). Start at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`, which part 1 (BL-3887) also changes, so it runs after it. Measure real curl before pinning text.

## Acceptance criteria

- [x] `curl --proto =smb -sS smb://127.0.0.1:20002/share/x` through real curl and through Curl gives the same exit code (2), stdout bytes, stderr text and no request bytes on the wire; commands and both results are recorded in Notes.
- [x] A focused unit test in `Curl.Cli.UnitTests/Cgr043SmbProtoTests.cs` fails before the fix and passes after it (ADR-0482).
- [x] Repro variants that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3572, which went over its cost cap.
- Measured 2026-10-26 with `Record-CurlExchange.ps1 -Port 20002 -CurlArgs '--proto','=smb','-sS','smb://127.0.0.1:20002/share/x'`
  against the Windows curl (Schannel), then with `-Curl dotnet -CurlArgs 'run','--project','Curl.Console','--no-build','--',...`
  through Curl: both exit 2, stdout empty, request.bin empty (no byte on the wire), stderr
  `curl: unrecognized protocol 'smb'`, `curl: option --proto: is badly used here`, the try-help line.
  All four files hash-identical. Before the fix Curl accepted `=smb` (the old tests pinned that).
- Variant `curl -sS smb://127.0.0.1:20002/share/x` (part 1) re-measured: both exit 1 with
  `curl: (1) Protocol "smb" is disabled`, all four files identical.
- Decision: `--proto` and `--proto-redir` read against a private `ProtoSchemes` (`KnownSchemes` less
  smb/smbs) on every platform, as neither reference build has SMB (ADR-0835). `KnownSchemes` and
  `KnownScheme` keep smb for now: `--proto-default` is BL-3889's part, and
  `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1627SchemeLookup.cs` (upstream unit 1627)
  reads `KnownScheme` and lists SMB as known; BL-3889 must decide that one.
- Touches widened: `Curl.Cli.UnitTests/CommandLineProtocolSetOptionTests.cs` pinned smb as accepted by
  `--proto` (two tests removed, smb dropped from its every-scheme list), and ADR-0835 item 2 reworded so it
  says what the code does. No task in Doing on origin/work/dark-factory names either.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 0 failing members. Fast tests: 45
  test assemblies passed, 0 failed (Curl.Cli.UnitTests 4376 passed before the last nullable fix).
- CI on Linux and macOS: the change and tests are pure parsing, platform-neutral.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: f3cf8380 f05281c1 b084e04d 779790c0.
- 2026-10-26: Doing -> Done. --proto =smb and --proto-redir smb are refused as unrecognized protocol, exit 2, as curl 8.22.0 does
