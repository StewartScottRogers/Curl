---
id: BL-2413
title: Libcurl: GetInfo for long CURLINFO values
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2412]
touches: [Libcurl/Info/Longs, Libcurl.UnitTests/Info/Longs]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2413 — Libcurl: GetInfo for long CURLINFO values

## Goal

Every long `CURLINFO_*` of the plan's inventory (`RESPONSE_CODE`, `HTTP_CONNECTCODE`, `HTTP_VERSION`, `FILETIME(_T)`, `REDIRECT_COUNT`, sizes, verify results, auth masks, `OS_ERRNO`, ports, `CONDITION_UNMET`, RTSP counters, `PROTOCOL`, `PROXY_ERROR`, `USED_PROXY`, `XFER_ID`, `CONN_ID`, `LASTSOCKET`, `ACTIVESOCKET`) is answered by `GetInfo`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per info reads the value from a fake `TransferInfo`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: one `EasyInfoReaders` part in `Libcurl/Info/Longs` adding a named reader per long, socket and non-time `_OFF_T` info (`FILETIME_T`, `XFER_ID`, `CONN_ID`); the seam (`TransferInfo`) already had every property, so the plan needed no correction. `FILETIME` and `FILETIME_T` share one reader. No ADR: no design choice beyond reading the seam as is.
- Verified: `dotnet build` 0 warnings; Libcurl.UnitTests 558 passed (36 new). Only Libcurl.UnitTests references Libcurl, so no other test project and no ratchet case is affected; `PassingUpstreamCases.txt` unchanged. `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. GetInfo answers every long, socket and FILETIME_T/XFER_ID/CONN_ID CURLINFO from TransferInfo
