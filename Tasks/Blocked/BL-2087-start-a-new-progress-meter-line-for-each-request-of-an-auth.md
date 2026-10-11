---
id: BL-2087
title: Start a new progress-meter line for each request of an auth-retried transfer (upstream test1075, test1072)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2087 — Start a new progress-meter line for each request of an auth-retried transfer (upstream test1075, test1072)

## Goal

Curl draws a new progress-meter line for each request of an authentication-retried transfer, as curl 8.21.0 does, so the progress output of upstream test1075 and test1072 matches.

## Context

In curl each request of a retried transfer (a 401 or 407 followed by the authenticated request) starts its own progress-meter line; Curl draws one meter for the whole transfer (Curl.Console/ProgressMeterLines.cs, ParallelProgressMeter.cs, TransferProgressRecorder.cs). Read 8.21.0 `tests/data/test1075` and `test1072` and measure real curl with `Record-CurlExchange.ps1` to pin the exact stderr bytes (header line, and what the first line shows when its request ends in a 401) before changing anything. Find where the retry reaches the console's progress recorder and start a fresh line there.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2074 (test1075) and BL-2075 (test1072). Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] New MSTests in Curl.Console.UnitTests (no network, platform-neutral) show a transfer whose first request is answered 401 and retried produces two meter lines with the stderr bytes real curl 8.21.0 gives, and a transfer without a retry still produces one.
- [ ] `--ai-help` needs no change (no option is added or changed); the task Log says so.
- [ ] Add test1075 and, once the rest of its output matches, test1072 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- 2026-10-11, lane 3 (no code changed): measured and located the cause before any edit.
  - Real curl 8.21.0 (Schannel, Git for Windows), `Record-CurlExchange.ps1 -Connections 2`
    with a `401` (`WWW-Authenticate: Basic`, `Content-Length: 0`, kept alive) to
    `-T <46-byte file> -u testuser:testpass --anyauth`: standard error is the two header
    lines (each `\r\n`), then one status line per request, each starting `\r  0 ...` (the
    zero line) and ending `\r\n` after its done draws (`\r100    46    0     0  100    46 ...`).
    That run sent 3 requests (3 lines). So each request of the retried transfer draws its
    own zero line and done lines, exactly as a redirect hop does (`FinishRedirectHop`).
  - Cause in Curl: `TransferProgressRecorder.ReportTransferStarted` already starts a new
    line on a repeat report, and `HttpProtocolHandler.ConnectAndExchangeAsync` (line ~662)
    reports it once per connection. A retry on the same kept-alive connection
    (`RetryOnSameConnection` loop, line ~976) never reports it again, so Curl draws one
    line for the whole transfer. The fix is to call `plan.Progress.ReportTransferStarted()`
    for each same-connection retry in `Curl.Protocol.Http.UnitLibrary`, plus a Console
    test pinning two lines; probably no `Curl.Console` change at all.
  - test1075 and test1072 already pass in process (`UpstreamCase_RunThroughCurl_HoldsTheRatchet`
    reports "passes"), since upstream does not check stderr; adding them to
    `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` needs that project.
  - Added `Curl.Protocol.Http.UnitLibrary`, `Curl.Protocol.Http.UnitTests` (held by BL-2085)
    and `Curl.Conformance.UnitTests` (held by BL-2093) to `touches`, so requeued.
  - `--ai-help` needs no change: no option is added or changed.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. touches Curl.Protocol.Http.UnitLibrary/UnitTests (held by BL-2085) and Curl.Conformance.UnitTests (held by BL-2093); cause and measurements in Notes
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 1.16 of its 2.10 US dollar cost cap, so it was not run again; split the task; see Z:\repos\Curl.logs\BL-2087-*.jsonl
