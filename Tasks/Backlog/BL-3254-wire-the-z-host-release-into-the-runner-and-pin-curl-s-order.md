---
id: BL-3254
title: Wire the -Z host release into the runner and pin curl's order with a holding fake server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3253]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelTransferQueue.cs, Curl.Console/ParallelHostQueue.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3254 — Wire the -Z host release into the runner and pin curl's order with a holding fake server

## Goal

`curl -Z` against one instant server writes stdout in curl 8.22.0's order for `/echo /default /utf8 /four`, and the full fast run of Curl.Console.UnitTests ends.

## Context

Split from BL-3189. BL-3253 gives `ParallelHostQueue`/`ParallelTransferQueue` the one-at-a-time release. Here `CurlCommandRunner.NewParallelRun` sets `BeforeEachWait` to `run.Hosts.StartReleased` (keep the runner change small). Curl 8.22.0 gives `/echo /utf8 /four /default` at the default `--parallel-max`, `/echo /utf8 /default /four` at 2, URL order at 1. The earlier test failed at 50 and 2 because `ScriptedResultHandler` answers synchronously, so `/echo` ended before `/default` waited on the host: the test needs a handler that holds `/echo` until the other three are waiting on the host (or started), then answers the rest at once. Also known: the full fast run of Curl.Console.UnitTests hung over an hour with the stashed work; first suspect is a freed transfer waiting for `StartReleased` while the runner is not inside a `ParallelTransferQueue` wait. Find it with `--blame-hang-timeout 60s` and the Sequence file's last test.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs` with a fake server answering instantly after holding `/echo` shows stdout order `/echo /utf8 /four /default` at the default `--parallel-max`, `/echo /utf8 /default /four` at 2 and URL order at 1, for `/echo /default /utf8 /four`.
- [ ] `dotnet build Curl.Console -warnaserror` is clean.
- [ ] `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes, finishes without hanging, with the existing `-Z` tests unchanged.

## Notes

- Split from BL-3189 (over its cost cap). Depends on BL-3253, which holds the stashed work; take the hang's cause into account when wiring.

## Log

- 2026-10-23: Created.
