---
id: BL-2414
title: Libcurl: GetInfo for time, size and speed CURLINFO values
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2412]
touches: [Libcurl/Info/Times, Libcurl.UnitTests/Info/Times]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2414 — Libcurl: GetInfo for time, size and speed CURLINFO values

## Goal

Every time (double and `_T` microsecond), size, speed and content-length `CURLINFO_*` (`TOTAL_TIME(_T)` ... `RETRY_AFTER`, `SIZE_UPLOAD(_T)`, `SPEED_DOWNLOAD(_T)`, `CONTENT_LENGTH_*`) is answered by `GetInfo`, the double forms derived from the same microsecond value as upstream.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per info; a double and its `_T` twin agree.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- `Libcurl/Info/Times/EasyInfoReaders.Times.cs` implements `AddTimeReaders` for 30 infos: the 7 double times and their `_T` twins, `POSTTRANSFER_TIME_T`, `QUEUE_TIME_T`, `RETRY_AFTER`, `EARLYDATA_SENT_T` (a size, which `TransferInfo` and the plan list with the times; BL-2413 does not name it), and the size, speed and content-length doubles and `_T`s. `FILETIME_T`, `XFER_ID` and `CONN_ID` are off_t but belong to BL-2413's long inventory, so they stay there.
- Default taken: a `TimeSpan` becomes microseconds by truncation (`Ticks / TicksPerMicrosecond`), since upstream's `timediff_t` is whole microseconds; the double is that count `/ 1000000.0`, upstream's `DOUBLE_SECS`. `RETRY_AFTER` is truncated whole seconds. No ADR: it follows `getinfo.c` directly.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members; Libcurl.UnitTests 460 passed; full fast run green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. GetInfo answers every time, size, speed and content-length CURLINFO, double forms derived from the same microseconds as upstream
