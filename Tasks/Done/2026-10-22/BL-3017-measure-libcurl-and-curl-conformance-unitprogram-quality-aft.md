---
id: BL-3017
title: Measure Libcurl and Curl.Conformance.UnitProgram quality after the protocol-mask reader and the four Dns/Easy ports and close any gap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3014]
touches: [Libcurl/Info/Longs/EasyInfoReaders.Longs.cs, Libcurl.UnitTests/Info/Longs/EasyInfoReadersLongsTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy, Curl.Conformance.UnitProgram.UnitTests/Dns/Easy]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3017 — Measure Libcurl and Curl.Conformance.UnitProgram quality after the protocol-mask reader and the four Dns/Easy ports and close any gap

## Goal

`Libcurl` and `Curl.Conformance.UnitProgram.UnitLibrary` keep 100% line and branch coverage, complexity at most 10 and CRAP at most 30 after BL-3014 adds the masked protocol reader and the four `Dns/Easy` programs.

## Context

Split from BL-2925. Run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Libcurl` and again with `Curl.Conformance.UnitProgram.UnitLibrary` (the script's header gives the options). Close any gap in `ReadProtocol` (`Libcurl/Info/Longs/EasyInfoReaders.Longs.cs`) or in `Dns/Easy/Unit1652InfoLineTruncation.cs`, `Unit2413ConnectionPeer.cs`, `Unit3219ProtocolMask.cs` and `Unit3302GssApiDelegation.cs` with focused tests in `Libcurl.UnitTests/Info/Longs/EasyInfoReadersLongsTests.cs` and `Curl.Conformance.UnitProgram.UnitTests/Dns/Easy/`; add a file to `touches` in the same commit if a gap lies elsewhere. Never change `CodeMetricsConfig.txt`. A gap BL-3014 did not cause goes in Notes and a separate task.

## Acceptance criteria

- [x] Measure-CodeQuality reports no member of the files named above outside the four gates.
- [x] Each test added fails when the branch it covers is removed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Libcurl.UnitTests` and `Curl.Conformance.UnitProgram.UnitTests`.

## Notes

- Measured both libraries together: Libcurl 100/100, 0 failing members (worst CRAP 10, ReadProtocol inside the gates). Curl.Conformance.UnitProgram.UnitLibrary 100/100 with one failure: `Unit2413ConnectionPeer.Mismatch` complexity 18. Split it into NameMismatch, AddressMismatch and ZoneIdMismatch (plus AsCFlag) keeping the C helper's check order and wording; re-measured 0 failing members, worst CRAP 10.
- No test added: the existing UnitProgramsDnsEasyTests still cover every branch (100% line and branch after the split), so there is no new branch for a test to pin.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e28e0b09 3ae47afb 8df27ce5.
- 2026-10-21: Doing -> Done. Both libraries inside all four gates; Unit2413ConnectionPeer.Mismatch split to complexity under 10.
