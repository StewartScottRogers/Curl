---
id: BL-3178
title: Start --parallel transfers in URL order and pick up waiting ones in URL order
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3181]
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

- [x] Notes record the start order and stdout order of curl 8.22.0 for the repro, `--parallel-max 1` and `--parallel-max 2`.
- [ ] BL-3181 is Done: its tests pin curl 8.22.0's measured order (ADR-0768) for the repro, `--parallel-max 1` and `--parallel-max 2`, in place of the URL order this task first asked for.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; existing `-Z` tests unchanged.

## Notes

- 2026-10-22, lane 2: measured with `Record-CurlExchange.ps1` (loopback 127.0.0.1:47811, each
  connection answered at once with an empty 200 and closed; `-Z --parallel-max N -s -w '%{url} '`;
  URLs `/echo /default /utf8 /four` to one host; three runs each; request order = stdout order):
  - curl 8.22.0, every run: max 50 `/echo /utf8 /four /default`; max 2 `/echo /utf8 /default /four`;
    max 1 `/echo /default /utf8 /four`. With only three URLs at max 50: `/echo /utf8 /default`.
  - Curl at 02ec93908: max 50 `/echo /utf8 /default /four`, `/echo /default /utf8 /four`,
    `/echo /default /four /utf8` (a race); max 2 `/echo /default /utf8 /four`; max 1 URL order.
  - So the premise is wrong: curl does not start in URL order, and what Stewart saw from Curl is
    curl's own three-URL order. Decided in ADR-0768 to match curl's measured order (the first waiter
    on the host starts behind the transfers starting with it) and make it deterministic.
  - A keep-alive measurement through the recorder's `-Script` mode hung for 30 minutes and was
    stopped; BL-3181 measures it with its own server.
  - Requeued: the implementation is filed as BL-3181 (too little time left after measuring for the
    change plus a 30 to 45 minute quality run). The three criteria that asked for URL order contradicted
    the measurement and were replaced, after the move to Backlog, by one: BL-3181 is Done.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 184a969c 65c3687b c7498130 cc00745f 34f328d9.
- 2026-10-22: Doing -> Backlog. Waits for BL-3181: curl 8.22.0 measured not to start -Z transfers in URL order (ADR-0768); the implementation of its measured order is BL-3181
