---
id: BL-3181
title: Start -Z transfers to one host in curl 8.22.0's measured order, one after another
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelTransferQueue.cs, Curl.Console/ParallelHostQueue.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs, Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs, Curl.Console.UnitTests/ParallelTransferQueueTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3181 — Start -Z transfers to one host in curl 8.22.0's measured order, one after another

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

- [ ] Notes record curl 8.22.0's order against a keep-alive loopback server for the four URLs at `--parallel-max` 50, 2 and 1.
- [ ] A test in `Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs`, with a fake `TimeProvider` and no sleeping, shows that when a host's first transfer ends its waiters are released in the ADR-0768 order (first waiter last), one at a time.
- [ ] A test in `Curl.Console.UnitTests/ParallelTransferQueueTests.cs` shows the transfer started when a slot frees is the earliest-waiting one.
- [ ] A test in `Curl.Console.UnitTests/CurlCommandRunnerParallelOrderTests.cs` with a fake server answering instantly shows stdout order `/echo /utf8 /four /default` at the default `--parallel-max`, `/echo /utf8 /default /four` at 2 and URL order at 1, for `/echo /default /utf8 /four`.
- [ ] `dotnet build Curl.Console -warnaserror` is clean, `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes with the existing `-Z` tests unchanged, and Measure-CodeQuality.ps1 shows Curl.Console inside every gate.

## Notes

## Log

- 2026-10-22: Created.
