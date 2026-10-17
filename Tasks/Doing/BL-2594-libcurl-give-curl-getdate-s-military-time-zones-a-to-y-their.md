---
id: BL-2594
title: Libcurl: give curl_getdate's military time zones A to Y their upstream signs (test517)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2594 — Libcurl: give curl_getdate's military time zones A to Y their upstream signs (test517)

## Goal

`CurlDate.Parse` (`curl_getdate`) gives the single-letter military time zones A to I and K to Y the offsets curl 8.22.0 gives them, so every date in lib517.c parses as upstream expects.

## Context

Found by BL-2528 (lib517 port, `Curl.Conformance.LibTest.UnitLibrary/Lib517.cs`). curl 8.22.0 parses `06 Nov 1994 08:49:37 A` to 784108177 and `... N` to 784115377; Curl has every sign reversed: `WRONGLY 06 Nov 1994 08:49:37 A => 784115377 (instead of 784108177)`, and so on for B to I and K to Y. Every other date in the table matches. test517 has no `<client><command>`, so it also waits on BL-2589 before it runs through the runner.

## Acceptance criteria

- [ ] A focused unit test in `Libcurl.UnitTests` pins A, M, N and Y against the lib517.c table.
- [ ] `Lib517.CheckDatesAsync` over `Lib517.Dates` prints no `WRONGLY` line.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 3f80acba acfea394 4272a6b9 c84701f0.
