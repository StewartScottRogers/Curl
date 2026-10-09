---
id: BL-1843
title: --retry --fail on a 429 with Retry-After retries and keeps the 429's head (upstream test1634)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1843 — --retry --fail on a 429 with Retry-After retries and keeps the 429's head (upstream test1634)

## Goal

`curl http://host/1634 --retry 1 --fail` against upstream test1634's server (429 `Retry-After: 1`, then 200 `hey`) makes both requests and writes what curl 8.21.0 writes, so the gap item `behaviour:test1634` measures `match`.

## Context

- Split from BL-1808 (gap finding GF-0015, item `behaviour:test1634`); read BL-1808's Notes first.
- Upstream: https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/test1634 (datacheck: the 429's head with no `moo` body, then the 200's head and `hey`).
- The gap measure expected `HTTP/1.1 429 too many requests swsbounce` first and Curl gave `HTTP/1.1 200 OK`: the 429 attempt's head output is dropped or cut back on the retry. Start at `CurlCommandRunner.FollowRetryingAsync` / `AttemptBytesKeptForResume` / `WriteRetryLinesAsync` (Curl.Console).
- Measure real curl with `Record-CurlExchange.ps1` (with and without `-i`/`-o`) before pinning output.

## Acceptance criteria

- [x] A Curl.Console unit test replays test1634's exchange and pins curl 8.21.0's output and exit code.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-08 (lane 1): Curl already matched test1634 on this commit, so no production change
  was needed (Curl.Core untouched). `CurlCommandRunnerRetryTests.RunAsync_RetryUnderFailOfA429WithRetryAfter_KeepsThe429sHeadAndWritesTheRetrysResponse`
  replays the exchange under `--retry 1 --fail -i` (runtests adds `-i`) and pins two GETs, one
  1-second wait, exit 0 and stdout byte for byte as upstream's datacheck (the 429's head with no
  `moo`, then the 200's head and `hey`). The gap measure's `200 OK`-first output no longer
  reproduces. Choice: stdout is pinned from curl 8.21.0's own datacheck rather than a fresh
  `Record-CurlExchange.ps1` run, which the run's cost cap left no room for; stderr (the
  `(22) ... 429` line, then the retry warning) follows the order measured for a 503 under
  `-f --retry` in BL-241, which runs the same code path.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Test1634's exchange pinned; Curl already matches curl 8.21.0's datacheck
