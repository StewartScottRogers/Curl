---
id: BL-2319
title: Read the --write-out variables from TransferInfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2317, BL-2318]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2319 — Read the --write-out variables from TransferInfo

## Goal

`-w` (`--write-out`) takes every libcurl-measured variable from the transfer's `TransferInfo`, as upstream's tool takes them from `curl_easy_getinfo`; the tool's own variables (`%{urlnum}`, `%{filename_effective}`, `%{exitcode}`, `%{errormsg}`, `%{onerror}`, `%{stderr}`/`%{stdout}`, headers through the header API) stay where they are; output is byte for byte unchanged.

## Context

ADR-0506, plan phase 2 step 5; needs BL-2317 and BL-2318. `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` builds the variables today from the transfer report and runner state (`CurlCommandRunner.WriteOutVariables`). `Curl.Output.UnitLibrary` gains a reference to `Curl.Api.UnitLibrary` (allowed: the seam references no protocol library). Upstream's split: `src/tool_writeout.c` reads `CURLINFO_*` for each libcurl variable.

## Acceptance criteria

- [x] `TransferWriteOutVariables` (or its successor) is built from `TransferInfo` for every variable upstream reads with `curl_easy_getinfo`; a test per variable group (times, sizes, speeds, addresses, HTTP, TLS, redirects, IDs) builds it from a `TransferInfo` and checks the formatted text.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the existing `-w` tests in `Curl.Console.UnitTests` and `Curl.Output.UnitTests` pass unchanged).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- Design (ADR-0553): `Curl.Output` gains `WriteOutTransferInfo.FromReport`, the one report-to-`TransferInfo` conversion for the fields `-w` reads; `Curl.Engine`'s `TransferInfoFromReport` now starts from it and adds only the `CURLINFO_*` values no `-w` variable reads, so `-w` and `curl_easy_getinfo` cannot drift apart. `TransferWriteOutVariables` has a new `(TransferInfo, TransferResult, url, urlNumber)` constructor and reads every libcurl variable from the info; the report-only constructor stays (its tests pass unchanged) and builds the info with `FromReport`. `CurlCommandRunner.WriteOutVariables` builds the info and puts the run's scheme, Referer, conn/xfer IDs, verify results and early data on it.
- Default taken: the runner builds the info itself rather than reading the easy handle's `Info`, because `-Z` has no handle yet and the handle's attempt wiring is BL-2394's; it is the same conversion either way.
- `TransferInfo` answers a port with no connection as 0 (libcurl: -1), so `%{remote_port}` prints -1 for primary port 0 and `%{local_port}` keeps its -1/0 distinction from the primary port: the same bytes for every report a transfer makes.
- Still read from the report or result (tool-side, or held in another form in `TransferInfo`): size_delivered, num_headers, the headers, certs/num_certs, exitcode, errormsg, url, urlnum, num_retries, filename_effective.
- Measured: build clean; fast tests green in all 39 test projects (Engine 1007, Console 2789, Conformance 3130; the ratchet file unchanged). Measure-CodeQuality for Curl.Output.UnitLibrary, Curl.Engine.UnitLibrary and Curl.Console: 100% line and branch, 0 failing members (a long-constant switch in `FormatHttpVersion` left a compiler branch no input reaches; it is a lookup table now). `CurlEasyErrorText.cs` and `InProcessCurl` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. -w reads every libcurl variable from a TransferInfo built by one shared conversion; output bytes unchanged
