---
id: BL-1842
title: --retry keeps a retried attempt's output on standard output and resends a -d POST from the first URL under -L (upstream test1633)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1842 — --retry keeps a retried attempt's output on standard output and resends a -d POST from the first URL under -L (upstream test1633)

## Goal

`curl http://host/1633 -d moo --retry 1 -L` against upstream test1633's server sends POST /1633, GET /16330002 (429, Retry-After: 1), then POST /1633 with the body again and GET /16330002 again, as curl 8.21.0 does, and writes all four responses' output.

## Context

- Split from BL-1808 (gap finding GF-0015, item `behaviour:test1633`); read BL-1808's Notes first.
- Upstream: https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/test1633 (data: 301 to /16330002; data2: 429 with `Retry-After: 1`; datacheck: 301, 429, 301, 429).
- The gap measure stopped at byte 177: Curl wrote the 301 and the 429 and then nothing, so either no retry was made after the redirect's last hop or the retry wrote nothing. Start at `CurlCommandRunner.FollowRetryingAsync` (Curl.Console) and `TransferRetrier` (Curl.Core; the last hop's scheme decides, `LastScheme`).
- Check against real curl with `Record-CurlExchange.ps1` before pinning anything.

## Acceptance criteria

- [x] A Curl.Console unit test replays test1633's exchange and pins the four requests (POST with `moo` twice) and the stdout of curl 8.21.0.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-08 (lane 1): Measured with `Record-CurlExchange.ps1 -Connections 4` (four
  connections are needed; with the default one the recorder hangs), answering 301, 429, 301,
  429 as test1633 does, under `-d moo --retry 1 -L -i` (runtests adds `--include`, which is
  why test1633's datacheck holds the heads). curl 8.21.0: four requests (POST /1633 with `moo`,
  GET /16330002, the same again), all four heads on stdout, one `Retrying in 1 second` warning,
  exit 0. Curl's current build, through the same recorder, sent and wrote the same bytes and
  exited 0, so no production change was needed: the gap measure's stop at byte 177 no longer
  reproduces on this commit. `CurlCommandRunnerRetryTests.RunAsync_RetryOfARedirectedPostAnswered429_ResendsThePostFromTheFirstUrl`
  now pins the four requests, the stdout and the one 1-second wait. Curl.Core needed no change.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Test1633's exchange pinned; Curl already matches curl 8.21.0 byte for byte
