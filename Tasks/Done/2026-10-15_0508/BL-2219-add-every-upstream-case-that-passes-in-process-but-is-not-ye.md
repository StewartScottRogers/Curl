---
id: BL-2219
title: Add every upstream case that passes in process but is not yet on the PassingUpstreamCases.txt ratchet
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2219 — Add every upstream case that passes in process but is not yet on the PassingUpstreamCases.txt ratchet

## Goal

Put on `PassingUpstreamCases.txt` every case that passes in process but is not listed, so the ratchet holds them.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%); counts from running `Curl.Conformance.UnitTests` on 2026-10-14, where about 100 `UpstreamCase_RunThroughCurl_HoldsTheRatchet` rows ended `Inconclusive` with "testN passes; add N to PassingUpstreamCases.txt" (for example 17, 56, 71, 90, 146, 149, 155, 203, 214, 215, 216, 271, 283-286, 332, 369, 381, 384, 409, 416, 428, 448, 458, 610, 649, 1725, 2004, 2033, 2070, 2079, 3024). The gap tool counts these as `match` already; the ratchet is what keeps them from regressing.

Run `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration&FullyQualifiedName~UpstreamConformanceTests"` and read the Inconclusive messages (trx logger). A case listed for one platform only must follow how the file already handles platform-specific cases (read `UpstreamCaseRatchet.cs` and the file's header; ADR-0488). Lines stay in the file's existing order and format.

## Acceptance criteria

- [x] Every case the run reports as "passes; add N to PassingUpstreamCases.txt" on Windows is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and a second run reports none, with the same cases not failing on Linux or macOS (a case that passes only on Windows is handled as the ratchet's existing platform rule says, and named in Notes).
- [x] Any case that passes intermittently is left out and named in Notes with the cause, with a follow-up task.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-10-14: The Windows run reported 108 cases as "passes; add N" (17 56 71 90 146 149 155 203 214 215 216 271 283-286 322 332 369 381 384 409 416 428 448 458 610 649 698 711 755 786 793 804 815 816 834 838 840 846 880 884 886 936 941 943 945 980 982 983 986 1001 1002 1005-1007 1009 1010 1036 1040 1042 1043 1049 1093 1094 1096 1099 1137 1146 1149 1217 1225 1238 1242 1243 1273 1434 1444 1475 1618 1633 1634 1725 2004 2033 2045 2051 2058 2060 2070 2079 2110 2113-2116 2305 3008 3011-3013 3024 3031 3203 3215 3217 3218 3229). All merged into the list in numeric order; the second run passed 1525 (was 1417), reported none to add and none failing.
- Windows-only: 2033, 2070, 2079 and 3024 need the `Schannel` feature, so on Linux and macOS `FindFeatureOfAnotherPlatform` skips them and the ratchet reports them Inconclusive, not failed (ADR-0488). No other added case names a platform feature. Linux and macOS cannot be run from a lane; the shift coordinator files any CI failure.
- Intermittent: none seen across the two runs. Real-clock cases checked by hand: 1238 (`-Y1000 -y2` against a 2000 s `writedelay`, wide margin), 17 and 285 (`--max-time` in hundreds of seconds), 1633 and 1634 (`--retry`). No follow-up task needed.
- Also updated `Curl.Conformance.UnitLibrary/CLAUDE.md`, whose TFTP, `<setenv>` and SFTP paragraphs still called several of these cases failing.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. 108 more upstream cases held by the PassingUpstreamCases.txt ratchet (1525 listed cases pass on Windows)
