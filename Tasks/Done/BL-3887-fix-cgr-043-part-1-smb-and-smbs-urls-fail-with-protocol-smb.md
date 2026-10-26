---
id: BL-3887
title: Fix CGR-043 part 1: smb:// and smbs:// URLs fail with Protocol "smb" is disabled (exit 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043SmbUrlTests.cs, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitLibrary/DisabledProtocolHandler.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/EngineCompositionTests.cs, Curl.Engine.UnitTests/DisabledProtocolHandlerTests.cs, Curl.Engine.UnitTests/Cgr043SmbUrlTests.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Console.UnitTests/CurlCompositionTests.cs, Curl.Console.UnitTests/CurlCommandRunnerSmbTransferEventTests.cs, Curl.Conformance.UnitLibrary/UpstreamSmbFront.cs, Curl.Conformance.UnitTests/CurlTestCases/Extended/SMB, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/SmbServerConnectorTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.SmbCases.cs, Curl.Conformance.UnitTests/UpstreamStandInFrontsTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3887 — Fix CGR-043 part 1: smb:// and smbs:// URLs fail with Protocol "smb" is disabled (exit 1)

## Goal

On Windows `curl -sS smb://127.0.0.1:20002/share/x` (and the `smbs://` form) behaves as curl 8.22.0 Schannel does: stderr `curl: (1) Protocol "smb" is disabled`, exit 1, no connection attempted, since smb and smbs are not in the `-V` protocol list.

## Context

Part 1 of 3 split from BL-3572 (CGR-043 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, related GF-0037, GF-0091). Today Curl tries to connect and gives exit 7. Start at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs` and the SMB protocol registration. Rebuild the repro with `Record-CurlExchange.ps1` (no Python); measure real curl before pinning text. Parts 2 and 3 (BL-3888, BL-3889) cover `--proto =smb` and `--proto-default smb`.

## Acceptance criteria

- [x] `curl -sS smb://127.0.0.1:20002/share/x` through real curl and through Curl gives the same exit code (1), stdout bytes, stderr text and no request bytes on the wire; commands and both results are recorded in Notes.
- [x] The `smbs://` form is measured the same way and matches; the result is recorded in Notes.
- [x] A focused unit test in `Curl.Cli.UnitTests/Cgr043SmbUrlTests.cs` fails before the fix and passes after it (ADR-0482), and is platform-neutral or marked for Windows.
- [x] Repro variants that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3572, which went over its cost cap.
- Continues the stashed work of BL-3572.
- Measured 2026-10-26, real curl 8.22.0 (x86_64-w64-mingw32, Schannel) and Curl (`Curl.Console/bin/Debug/net10.0/curl.exe`):
  `curl -sS smb://127.0.0.1:20002/share/x` -> both exit 1, stdout 0 bytes, stderr `curl: (1) Protocol "smb" is disabled`;
  `curl -sS smbs://127.0.0.1:20002/share/x` -> both exit 1, stdout 0 bytes, stderr `curl: (1) Protocol "smbs" is disabled`;
  `-v smb://...` -> both `* Protocol "smb" is disabled` then the `curl: (1)` line. No connection is opened: the refusal
  comes before any connect, which `Cgr043SmbUrlTests` pins with a recording connector that sees no target.
- Fix (ADR-0829, renumbered from the stash's ADR-0819, which other lanes took): `EngineComposition` serves smb and smbs with
  `DisabledProtocolHandler` (from the BL-3572 stash). The SMB end-to-end tests that drove Curl's SMB client through the
  composed curl went with it (ext11002-ext11005, `UpstreamConformanceTests.SmbCases.cs`,
  `CurlCommandRunnerSmbTransferEventTests.cs`); ext11001 now pins the refusal, upstream 1451 and 1850 leave the ratchet
  (no `smb` feature, as the reference build), and `SmbServerConnectorTests` drives `SmbProtocolHandler` directly.
  `touches` grew by those files; no task in Doing on origin/work/dark-factory named any of them.
- Left to BL-3888 and BL-3889: `CommandLineProtocolSet.KnownSchemes` still lists smb and smbs, so this run did not change
  `CommandLineProtocolSet.cs`; dropping them (with `LibcurlSourceCode`'s filter, `unit1627`'s lookup and the stash's
  `Cgr043Tests`) is those tasks' work. The stash's `Cgr043Tests.cs` is in stash 10c099e5 for them.
- The focused test is `Curl.Engine.UnitTests/Cgr043SmbUrlTests.cs`, not `Curl.Cli.UnitTests`: Curl.Cli.UnitTests references only
  Curl.Cli.UnitLibrary and cannot reach the composed handlers where the fix lives. It failed before the fix (exit 7,
  `Failed to connect`) and passes after; it is platform-neutral (no path, OS text or socket).
- Repro variants that already matched: `--proto` and redirect "is disabled" tests in `CurlCommandRunnerRedirectTests` and
  `ProtocolDispatcher` tests still pass; the whole fast run passed (46 test projects, 0 failed).
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: `DisabledProtocolHandler` fully covered; the one failing member,
  `MultiCallbackQueue.QueuedCallback.WaitUntilRun`, is not touched here and is BL-3857's.
- CI on Linux and macOS: the change is platform-neutral and the fast tests are green here.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 97a232e4 9e6c559a 5a7f2942 b084e04d 779790c0.
- 2026-10-26: Doing -> Done. smb:// and smbs:// URLs fail with exit 1 and Protocol "smb" is disabled before connecting, as curl 8.22.0 does
