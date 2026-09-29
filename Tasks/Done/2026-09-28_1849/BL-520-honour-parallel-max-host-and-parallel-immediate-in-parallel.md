---
id: BL-520
title: Honour --parallel-max-host and --parallel-immediate in parallel runs
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-520 — Honour --parallel-max-host and --parallel-immediate in parallel runs

## Goal

In a `-Z` run no more than `--parallel-max-host` transfers talk to one host at once, and `--parallel-immediate` opens new connections at once rather than waiting to reuse one, as curl 8.21.0 does for HTTP/1.1 connections.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker). Builds on BL-519's scheduler and BL-518's ADR.
- Curl speaks HTTP/1.1 only (ADR-0017), so multiplexing never applies; `--parallel-immediate`'s observable effect is how many connections are opened and when. Measure it with `Record-CurlExchange.ps1 -Connections 3` (count accepted connections, `%{num_connects}` and `%{conn_id}` per transfer) with and without the option, and `--parallel-max-host 1` with three URLs on one host.

## Acceptance criteria

- [x] Measured first as above; the connection counts and `-w` values copied into Notes.
- [x] `Curl.Console.UnitTests` tests pin the per-host limit (never more than the limit concurrent for one host, other hosts unaffected) and the measured connection behaviour of `--parallel-immediate`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 Schannel, 2026-09-28)

`Record-CurlExchange.ps1 -Connections 3 -ResponseDelayMilliseconds 500` (one connection served at a
time), `-Z -s -v -w '%{urlnum} nc=%{num_connects} cid=%{conn_id} tc=%{time_connect} tp=%{time_pretransfer} tt=%{time_total}'`
with `http://127.0.0.1:P/a /b /c`:

- none: `0 nc=1 cid=0 tc=0.000617 tt=0.534`, `2 nc=1 cid=1 tc=0.534 tt=1.042`, `1 nc=1 cid=2 tc=0.000282 tp=0.534 tt=1.557`;
  `-v`: `/b` and `/c` "Found pending candidate for reuse and CURLOPT_PIPEWAIT is set / Waiting on
  connection to negotiate possible multiplexing" until `/a` ended. 3 connections accepted.
- `--parallel-immediate`: `tc` 0.000941, 0.000440, 0.000331 (all at once), `cid` 0, 1, 2, `nc=1` each. 3 connections.
- `--parallel-max-host 1`: `tc` 0.000966, 0.520898, 1.034418; `cid` 0, 1, 2; `nc=1` each. The same with `--parallel-immediate` added.
- none, response `Content-Length: 10` with a 2-byte body held open 1 s (exit 18 each): `/b` connects at
  1.356 s, when `/a` ended, not when its headers came (0.3 s).
- `--parallel-max 2 --parallel-max-host 1 --parallel-immediate`, `/c` on `localhost`: `/c` starts only
  once `/a` ends (`/b`'s `tt` 1.02 s is served second), so `/b` waiting for its host keeps its slot.
- With a URL on another host in the run, the pipe-wait sometimes ended early, woken by that host's
  connection events. This is a race in curl's event loop and is not reproduced.

### Decisions (ADR-0153, Decided by Claude under Stewart's delegation)

- `ParallelHostQueue` holds a transfer inside its `--parallel-max` slot. The host is `host:port` from
  `System.Uri`, and a URL with no host is never held.
- Without `--parallel-immediate`, only an `http://` transfer waits for the first transfer to its host
  to end. For `https://`, curl ends the wait at ALPN, which is instant compared with a transfer; other
  schemes never multiplex.
- `-L` hops count against the host of the transfer's own URL.
- The existing `CurlCommandRunnerParallelTests` URLs each got a host of their own (`http://a.h/a` and
  so on), so the ADR-0127 rows keep running all at once. Row 3's expected `%{url}` changed with them.
- `Documentation/Planning/Decisions` was added to `touches` for ADR-0153. No task in Doing names it.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -Z honours --parallel-max-host per host:port and, without --parallel-immediate, holds http transfers to a host until its first ends, as curl 8.21.0 does
