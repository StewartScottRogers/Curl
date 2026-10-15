---
id: BL-2282
title: Split the redirect-following transfer attempt out of CurlCommandRunner into a TransferPerformer class in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2282 — Split the redirect-following transfer attempt out of CurlCommandRunner into a TransferPerformer class in Curl.Console

## Goal

The redirect-following attempt of one transfer - what libcurl does inside one `curl_easy_perform` - lives in a new `TransferPerformer` class in `Curl.Console`, and `CurlCommandRunner` calls it; behaviour is byte for byte unchanged.

## Context

ADR-0506, plan `Documentation/Planning/Curl-Api-And-libcurl.md` (phase 1, step 2). `Curl.Console/CurlCommandRunner.cs` (~4900 lines) mixes the curl tool's work with libcurl's. This is the first of three in-place splits (then BL-2283, BL-2284) that make the libcurl half one class, which BL-2291 later moves to `Curl.Engine.UnitLibrary`. It runs inside `Curl.Console` only, so it can start before the engine project exists.

Move into `TransferPerformer` (names as of 2026-10-15): `FollowWatchingSpeedAsync`, `CreateRetryAttemptContext`, `StartLowSpeedWatchdog`, `StartMaxTimeWatchdog`, `KeepRetriedConnectionIdWhenReused`, `ReportsNoConnectionOpened`, `RecordSerialAttemptStart`, `TakeNextAttemptIds` and the state they need. The `--retry` loop (`FollowRetryingAsync`, `WriteRetryLinesAsync`, `SettleRetriedOutputFileAsync` and the attempt-byte bookkeeping) stays in the runner: retrying is the curl tool's work (`tool_operate.c`), not libcurl's. `TransferPerformer` takes what it needs by constructor (no static state); `CurlComposition.CreateRunner` builds it.

## Acceptance criteria

- [ ] `Curl.Console/TransferPerformer.cs` exists and holds the methods named in Context (or their renamed successors); `CurlCommandRunner` no longer declares them and its `--retry` loop calls `TransferPerformer` for each attempt.
- [ ] `TransferPerformer` has its own tests in `Curl.Console.UnitTests` (`TransferPerformerTests`) covering a redirect followed, a low-speed abort (exit 28) and a max-time abort (exit 28), on fakes with an injected `TimeProvider`.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `Curl.Console/CLAUDE.md` names `TransferPerformer` and what it does.

## Notes

## Log

- 2026-10-15: Created.
