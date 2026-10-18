---
id: BL-2594
title: Libcurl: give curl_getdate's military time zones A to Y their upstream signs (test517)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2594 — Libcurl: give curl_getdate's military time zones A to Y their upstream signs (test517)

## Goal

`CurlDate.Parse` (`curl_getdate`) gives the single-letter military time zones A to I and K to Y the offsets curl 8.22.0 gives them, so every date in lib517.c parses as upstream expects.

## Context

Found by BL-2528 (lib517 port, `Curl.Conformance.LibTest.UnitLibrary/Lib517.cs`). curl 8.22.0 parses `06 Nov 1994 08:49:37 A` to 784108177 and `... N` to 784115377; Curl has every sign reversed: `WRONGLY 06 Nov 1994 08:49:37 A => 784115377 (instead of 784108177)`, and so on for B to I and K to Y. Every other date in the table matches. test517 has no `<client><command>`, so it also waits on BL-2589 before it runs through the runner.

## Acceptance criteria

- [x] A focused unit test in `Libcurl.UnitTests` pins A, M, N and Y against the lib517.c table.
- [x] `Lib517.CheckDatesAsync` over `Lib517.Dates` prints no `WRONGLY` line.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

- The table lives in `Curl.Protocol.Abstractions.UnitLibrary/CurlDateParser.cs` (`ZoneMinutesBehindUtc`), the one `parsedate` port `curl_getdate`, `-z` and cookie `Expires` share, so `touches` gained `Curl.Protocol.Abstractions.UnitLibrary` and `.UnitTests`; no other task in Doing names them (BL-2595 touches only the LibTest projects). Libcurl itself needed no production change.
- A to M are now 60 to 720 minutes ahead of UTC and N to Y 60 to 720 behind, matching every A to Y row of `Lib517.Dates` (A 784108177, N 784115377); Z stays 0 and J stays unknown. Pinned by `CurlDateTests.Parse_MilitaryZone_AnswersTest517sSeconds` (A, M, N, Y) and four rows in `CurlDateParserTests`.
- Fast tests: all 42 projects green. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 3f80acba acfea394 4272a6b9 c84701f0.
- 2026-10-17: Doing -> Done. Military zones A to Y now carry curl 8.22.0's signs; every test517 date matches.
