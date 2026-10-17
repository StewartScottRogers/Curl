---
id: BL-2452
title: Re-base Curl.Console: -w variables read through Libcurl GetInfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2451, BL-2413]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2452 — Re-base Curl.Console: -w variables read through Libcurl GetInfo

## Goal

The tool's `-w` variables read through `CurlEasy.GetInfo` instead of `TransferInfo` directly.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Every `-w` variable's output is unchanged, pinned by the existing write-out tests.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (ADR-0625): with `-w`, `FollowRetryingAsync` reads the serial handle's answers through `CurlEasy.GetInfo`
  after its last attempt and before `curl_easy_cleanup` (`EasyHandleWriteOutInfo.Read`, kept on
  `RunningTransferState.EasyWriteOutInfo`); `WriteOutVariables` uses them (`LibcurlWriteOutInfo`) when the reported
  result carries the very report the handle's last perform gave, else `WriteOutTransferInfo.FromReport` as before.
- Measured, not guessed: the engine's `Info` fills the `-w` fields from the same report the same way
  (`TransferInfoFromReport` starts from `WriteOutTransferInfo`), but its `xfer_id`/`conn_id` come from the engine's own
  `TransferPerformer`, and its effective URL falls back to `CURLOPT_URL`. So the runner's scheme, `Referer`, IDs, verify
  results and early data still lie over the answers, and the request URL stands in when the report names no
  effective URL. Moving those into the engine is BL-2495 (filed). `-Z` stays on `FromReport` until BL-2453.
- The plan's "-w through GetInfo" line is corrected by ADR-0625's split; `Libcurl/CLAUDE.md` needs no change (no
  Libcurl member was added); `Curl.Console/CLAUDE.md` describes the new read.
- Verified: `dotnet build` clean; fast tests all green (Curl.Console.UnitTests 2790 passed, Curl.Conformance.UnitTests
  3130 passed, `PassingUpstreamCases.txt` untouched); `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line,
  100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. -w variables of a serial transfer read through its CurlEasy.GetInfo answers (ADR-0625); output unchanged
