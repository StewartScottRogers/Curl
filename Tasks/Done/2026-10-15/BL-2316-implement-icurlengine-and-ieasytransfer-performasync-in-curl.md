---
id: BL-2316
title: Implement ICurlEngine and IEasyTransfer.PerformAsync in Curl.Engine.UnitLibrary over the easy option groups
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2310, BL-2312]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2316 — Implement ICurlEngine and IEasyTransfer.PerformAsync in Curl.Engine.UnitLibrary over the easy option groups

## Goal

`Curl.Engine.UnitLibrary` has `ManagedCurlEngine : ICurlEngine` and `ManagedEasyTransfer : IEasyTransfer`, whose `PerformAsync` runs one transfer from its `EasyOptions` through `TransferPerformer` and returns its `CurlCode` - the seam proven end to end.

## Context

ADR-0506, plan phase 2 step 4; needs BL-2310 (engine reads only the easy option groups) and BL-2312 (the contracts). Body bytes go to `EasyCallbackOptions`' write callback, headers to the header callback (each defaulting, as libcurl does, to discarding when unset - the tool sets them; libcurl's default of writing to `stdout` is `Libcurl`'s job in phase 3, not the engine's). Members added by BL-2313 to BL-2315 (multi, share, URL, MIME, WebSocket, headers, connect-only) may throw `NotSupportedException` here with a doc comment naming the phase 3 task area; they are not part of this task, and coverage counts the throw. `Duplicate`, `Reset`, `Pause` and the error text are implemented. `ManagedCurlEngine` builds what `EngineComposition` builds, with no static state, so two engines in one process do not share anything.

## Acceptance criteria

- [x] `ManagedCurlEngine` and `ManagedEasyTransfer` exist in `Curl.Engine.UnitLibrary` and implement the contracts.
- [x] `ManagedEasyTransferTests` in `Curl.Engine.UnitTests` run an HTTP GET on a fake connector and assert the body bytes the write callback received, the header lines the header callback received and `CurlCode.Ok`; a refused connection returns `CurlCode.CouldNotConnect`; `Reset` restores `new EasyOptions()`; `Duplicate` copies options and not results.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md` describes both classes.

## Notes

- Plan: `ManagedCurlEngine` builds one dispatcher per perform with `EngineComposition` over engine-owned connection, TLS session and DNS caches (no static state); `ManagedEasyTransfer.PerformAsync` builds the context with `TransferContextFactory` and performs through `TransferPerformer.FollowWatchingSpeedAsync` and a `RedirectFollower`; `CallbackWriteStream` hands body and header writes to the callbacks. Recorded in ADR-0541.
- Defaults taken: tool-only `EngineTransferSettings` fields with no easy option take libcurl defaults (silent, no protocol limits, no URL query); no URL is `UrlMalformat` with `No URL set`; a callback returning a short count is `WriteError`; `Pause` records and returns `Ok`; `UpkeepAsync` returns `Ok`.
- The criterion's `CurlCode.CouldNotConnect` is `CurlCode.CouldntConnect` (7) in `Curl.Api`; the refused-connection test asserts that.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` first flagged `PerformAsync` at complexity 14; split into `CreateContext` and `Finish`, re-measured 100% line, 100% branch, 0 failing, worst CRAP 10. Full fast suite green; `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedCurlEngine and ManagedEasyTransfer run an easy transfer from EasyOptions to the write and header callbacks and return its CurlCode
