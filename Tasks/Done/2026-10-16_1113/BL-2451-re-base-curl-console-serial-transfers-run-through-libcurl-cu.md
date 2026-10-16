---
id: BL-2451
title: Re-base Curl.Console: serial transfers run through Libcurl CurlEasy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2409, BL-2410, BL-2411, BL-2400, BL-2401, BL-2402, BL-2403, BL-2404, BL-2405, BL-2406, BL-2407, BL-2422, BL-2414, BL-2446, BL-2448, BL-2450, BL-2457, BL-2394]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2451 — Re-base Curl.Console: serial transfers run through Libcurl CurlEasy

## Goal

Curl.Console's serial transfers run through `Libcurl`'s `CurlEasy` (the tool is libcurl's client, as upstream's), its `EasyOptionsFromCommandLine` becoming `SetOption` calls.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] The output is byte for byte unchanged; `CurlCommandRunnerEasyTransferTests` shows one `Perform` per attempt.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (ADR-0624): `ToolCurlEngine` wraps the runner's `ICurlEngine` and holds a `Libcurl` `CurlLibrary` over itself;
  its `EasyInit` is `curl_easy_init` and pairs the `CurlEasy` with the engine's `IToolEasyTransfer` it just created
  (`ToolEasyHandle`), so the runner keeps the tool's hooks and `LastResult` without opening any `Libcurl` internal
  (the task's `touches` is Curl.Console only). Each serial transfer: one handle, the records on the hooks' `Options`,
  `CURLOPT_URL` through `CurlEasy.SetOption`, one `CurlEasy.PerformAsync` per attempt, cleanup at the end.
- Split: converting all ~150 option fields to `SetOption` calls, each checked against Libcurl's setters with the
  output unchanged, is more than one run. Filed BL-2491 (behaviour, transfer, network), BL-2492 (TLS,
  authentication, proxy) and BL-2493 (HTTP, protocols), each depending on this task.
- The formatter re-indented `FollowRetryingAsync` through `PerformThroughEasyAsync`, which sat at column 0; the
  whitespace-only part of the diff is that.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console` 0 failing members; fast tests all green (Curl.Console.UnitTests
  2786 passed; Curl.Conformance.UnitTests 3130 passed, ratchet unchanged).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Serial transfers run through a Libcurl CurlEasy (curl_easy_init, CURLOPT_URL via SetOption, curl_easy_perform per attempt, cleanup); remaining option groups filed as BL-2491..BL-2493
