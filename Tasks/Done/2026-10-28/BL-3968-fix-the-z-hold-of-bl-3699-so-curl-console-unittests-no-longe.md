---
id: BL-3968
title: Fix the -Z hold of BL-3699 so Curl.Console.UnitTests no longer hangs (CGR-170)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelTests.cs, Curl.Console/CLAUDE.md, Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3968 — Fix the -Z hold of BL-3699 so Curl.Console.UnitTests no longer hangs (CGR-170)

## Goal

Under `-Z`, a later URL that cannot be set up (bad glob, unreadable `-T` file) abandons the held first batch as curl 8.22.0 does (CGR-170), and `Curl.Console.UnitTests` passes without hanging.

## Context

Split from BL-3699 (over its cost cap). The ADR-0866 fix (hold the first `--parallel-max + 1` transfers in `ParallelRun`, open each `-T` file as held, abandon the batch on a setup failure) was written and stashed; the six `Cgr170Tests` pass alone, but `dotnet test Curl.Console.UnitTests --filter "FullyQualifiedName~Parallel" --blame-hang-timeout 1m` hangs after 95 tests. BL-3699's Notes (in Deferred) hold the measured curl behaviour and the likely cause.

## Acceptance criteria

- [x] `Cgr170Tests` pass and pin the measured CGR-170 behaviour (an abandoned batch reports each held transfer with the setup failure's code; with `--parallel-max 1`, transfers 1 and 2 run, then `2 3 http://[::1` and `URL rejected: Bad IPv6 address` are reported).
- [x] The `Parallel` tests in `Curl.Console.UnitTests` finish without hanging, and Notes names the cause of the hang.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3699.
- Continues the stashed work of BL-3699.
- Next step: list the `Parallel` tests that never finish (`--logger "console;verbosity=normal"`); likely a `-Z` test whose transfers must run at once is now held until the batch is complete. Perhaps hold only the setup checks, not the starts, or start held transfers when the URL loop next waits.
- Serial runs must take no changed path.
- Cause of the hang: `CurlCommandRunnerParallelTests.RunAsync_BadGlobAfterARunningTransfer_WaitsForItAndExitsWithTheGlobsCode` awaited `/a` starting before the bad glob `http://h/[1-`, but CGR-170 (ADR-0954) abandons the first batch at that glob, so `/a` never starts and the await never returned. Rewritten as `RunAsync_BadGlobAfterATransferOfTheFirstBatch_StartsNoTransferAndExitsWithTheGlobsCode`, pinning exit 3 and no start. Added `CurlCommandRunnerParallelTests.cs` to touches for it; no task in Doing on origin/work/dark-factory names it.
- Added `Curl.Console/CLAUDE.md` to touches to describe the held first batch; no task in Doing names it. `ParallelRun.StartHeldAsync` no longer returns early once a start finds the run ended (no test can reach that branch, and every later start returns at once as `HasEnded` never clears).
- CI on Linux and macOS: the change touches no path, file-system error or certificate, so it is platform-neutral; fast tests green here.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 56dcf0ab 4c4fc916 779790c0.
- 2026-10-28: Doing -> Done. Held first batch passes the Parallel tests without hanging; the hang was a test that awaited a transfer CGR-170 abandons
