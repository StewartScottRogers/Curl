---
id: BL-3189
title: Start -Z transfers to one host in curl 8.22.0's measured order, one after another
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/ParallelHostQueueTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelTransferQueue.cs, Curl.Console/ParallelHostQueue.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs, Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs, Curl.Console.UnitTests/ParallelTransferQueueTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3189 — Start -Z transfers to one host in curl 8.22.0's measured order, one after another

## Goal

`curl -Z` against one host starts its transfers, and so writes their stdout against an instant
server, in the order curl 8.22.0 does, the same on every run (ADR-0768).

## Context

BL-3178 measured curl 8.22.0 and Curl (table in ADR-0768, raw lines in BL-3178 Notes): with
`/echo /default /utf8 /four` to one `http://` host, curl gives `/echo /utf8 /four /default` at the
default `--parallel-max`, `/echo /utf8 /default /four` at 2, URL order at 1, every run. Curl races:
`ParallelHostQueue.Leave` releases every waiter of the host at once, each continuation
(`RunContinuationsAsynchronously`) runs on the thread pool and the connects race, so the order
changes run to run. Rule that fits curl: when the host's first transfer ends, the first transfer
that waited on the host (curl's pending one) starts behind every transfer that starts in that same
step (including one the freed `--parallel-max` slot starts in `CurlCommandRunner.StartInParallelAsync`);
the rest keep URL order. Released transfers start one after another, each sending its request
before the next begins.

Start by measuring a keep-alive server too (a C# file-based loopback server answering
`Content-Length: 0` without closing; the recorder's `-Script` mode hung for 30 minutes on it in
BL-3178, so do not use it for this, or give it a timeout), and extend the rule if curl differs there.
Keep the change in `CurlCommandRunner.cs` small; put the logic in the Parallel* files. Run
`Measure-CodeQuality.ps1` for Curl.Console once (30 to 45 minutes) before committing.

## Acceptance criteria

- [x] Notes record curl 8.22.0's order against a keep-alive loopback server for the four URLs at `--parallel-max` 50, 2 and 1.
- [ ] A test in `Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs`, with a fake `TimeProvider` and no sleeping, shows that when a host's first transfer ends its waiters are released in the ADR-0768 order (first waiter last), one at a time.
- [ ] A test in `Curl.Console.UnitTests/ParallelTransferQueueTests.cs` shows the transfer started when a slot frees is the earliest-waiting one.
- [ ] A test in `Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs` with a fake server answering instantly shows stdout order `/echo /utf8 /four /default` at the default `--parallel-max`, `/echo /utf8 /default /four` at 2 and URL order at 1, for `/echo /default /utf8 /four`.
- [ ] `dotnet build Curl.Console -warnaserror` is clean, `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes with the existing `-Z` tests unchanged, and Measure-CodeQuality.ps1 shows Curl.Console inside every gate.

## Notes

- 2026-10-22 (lane 8): measured curl 8.22.0 (Git for Windows ucrt64, Schannel) against a C# file-based
  keep-alive loopback server (`Content-Length: 0`, connection kept open, request path and connection
  number logged per request), `-s -Z -w '%{url} '` with `/echo /default /utf8 /four` to one host, three runs each:
  - `--parallel-max` 50: `/echo /default /utf8 /four` every run; server `conn1 /echo, conn1 /default, conn2 /utf8, conn3 /four`.
    So with keep-alive the pending transfer (`/default`) reuses the first connection and goes first: URL order.
  - 2: varies run to run (two connections race): `/echo /utf8 /default /four` (conn1 echo, conn2 utf8, conn1 default, conn2 four),
    `/echo /default /four /utf8` (conn1 echo, default, four; conn2 utf8), `/echo /utf8 /four /default`.
  - 1: `/echo /default /utf8 /four` every run, all on conn1.
  - Rule extension for the finishing claim: the pending transfer goes last only when the host's first
    connection was closed; when it is kept alive the pending transfer reuses it and keeps its place.
    Record it in a new ADR (amending ADR-0768) "Decided by Claude under Stewart's delegation".
- Touches widened to `Curl.Console.UnitTests/ParallelHostQueueTests.cs`: its tests call `Leave` and
  expect waiters released at once; with releases deferred to `StartReleased` each needs a
  `queue.StartReleased()` after `Leave`. No task in Doing on origin/work/dark-factory names it.
- Work in progress (left uncommitted for the shift's stash): `ParallelHostQueue.Leave` now moves the first
  waiter to the back at the host's first end and queues freed transfers in `released`; `StartReleased`
  starts them one after another (TCS without `RunContinuationsAsynchronously`, so each runs inline up
  to its first wait); a withdrawn freed transfer gives its host count back.
  `ParallelTransferQueue.BeforeEachWait` runs before each wait (slot wait and `WhenAllEndedAsync`, now a
  loop); `CurlCommandRunner.NewParallelRun` sets it to `run.Hosts.StartReleased`. New tests:
  `ParallelHostQueueOrderTests` (two), `ParallelTransferQueueTests` (one), `CurlCommandRunnerParallelOrderTests`.
- State when this claim ran out of time (22:50): build clean. `dotnet test --filter FullyQualifiedName~Parallel`
  ran 79 tests: 77 passed, `CurlCommandRunnerParallelOrderTests` failed at 50 and 2 with URL order
  (`/echo /default /utf8 /four`), because `ScriptedResultHandler` answers synchronously, so `/echo` ends
  before `/default` even waits on the host. The test needs a handler that holds `/echo` until the other
  three are waiting on the host (or started), then answers the rest at once. After the 79 the test host
  went inactive (blame hang at 60 s, no dump), and the full fast run of Curl.Console.UnitTests hung for
  over an hour: some -Z test now never ends. First suspect: a freed transfer that waits for
  `StartReleased` while the runner is not inside a `ParallelTransferQueue` wait (find it with
  `--blame-hang-timeout 60s` and the Sequence file's last test). Measure-CodeQuality.ps1 for Curl.Console not run yet.
- Chose to requeue this task itself rather than file the remainder as a new task: the uncommitted
  work is stashed against BL-3189, so only its next claim gets it back.
## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 6f5978ca e910298d 9f090ed0 11871184 f2813a19.
- 2026-10-22: Doing -> Backlog. Ran out of time: -Z order work stashed; the order test needs a holding fake handler and a -Z test now hangs (see Notes); next claim finishes it and runs Measure-CodeQuality
- 2026-10-23: Backlog -> Deferred. Over its cost cap; split into BL-3253, BL-3254, BL-3255
