---
id: BL-3178
title: Start --parallel transfers in URL order and pick up waiting ones in URL order
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelTransferQueue.cs, Curl.Console/ParallelHostQueue.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs, Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3178 — Start --parallel transfers in URL order and pick up waiting ones in URL order

## Goal

`curl -Z` starts transfers in URL order and picks the next one up in URL order whenever the parallel limit frees a slot, so stdout order follows URL order against an instant server.

## Context

Stewart reported on 2026-10-22: `curl -Z http://h/echo http://h/default http://h/utf8` (all short responses); upstream starts them in URL order, Curl sent `/echo`, `/utf8`, `/default`, so the standard-output order differs. Not yet measured by the coordinator: first reproduce with a loopback fake server that answers instantly and record the order curl 8.22.0 produces (with `Record-CurlExchange.ps1`, at default `--parallel-max` 50 and with `--parallel-max 1`, and with `--parallel-max 2` for four URLs). Start in `Curl.Console/CurlCommandRunner.cs` (`StartInParallelAsync`, near line 1806, awaits `run.WaitForFreeSlotAsync()` then starts the transfer), `ParallelTransferQueue.cs` (`WaitForFreeSlotAsync`) and `ParallelHostQueue.cs` (the per-host limit: a waiting transfer may be released out of order). Find which step reorders (a per-host wait releasing in arbitrary order, `Task.WhenAny`, or the connect race between the first and later transfers) and make the start order deterministic: first-come first-served in URL order. `CurlCommandRunner.cs` is a very large shared file; keep the change there small and put new logic in the Parallel* files.

## Acceptance criteria

- [ ] Notes record the start order and stdout order of curl 8.22.0 for the repro, `--parallel-max 1` and `--parallel-max 2`.
- [ ] A test in `Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs` with a fake server answering instantly shows stdout order equals URL order for three URLs, and the same under `--parallel-max 1`.
- [ ] A test of `ParallelHostQueue` (extend or add in `Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs`) shows waiters on one host are released in the order they arrived, with a fake `TimeProvider` and no sleeping.
- [ ] A `ParallelTransferQueue` test shows the transfer started when a slot frees is the earliest-waiting one.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; existing `-Z` tests unchanged.

## Notes

## Log

- 2026-10-22: Created.
