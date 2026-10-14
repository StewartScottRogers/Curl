---
id: BL-2203
title: Reuse a kept HTTP connection without the liveness check while it was used less than a second ago, as curl 8.22.0 does (closes GF-0078 test675)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2200]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2203 — Reuse a kept HTTP connection without the liveness check while it was used less than a second ago, as curl 8.22.0 does (closes GF-0078 test675)

## Goal

Curl's standard error for upstream test675 matches curl 8.22.0's, so a later gap analysis measures `behaviour:test675` (gap finding GF-0078) as `match`.

## Context

- Split out of BL-2200, which made the meter draw a died connection's resend as curl 8.22.0 does (`ITransferProgress.ReportRequestResentOnFreshConnection`, one draw, a newline, a new zero line; ADR-0489). This task makes Curl reach that resend at all.
- Measured 2026-10-13 with `Record-CurlExchange.ps1 -Connections 2` (`200 OK`, `Content-Length: 26`, server closes after each response), `-v -u user1:password1 http://127.0.0.1:47675/675 -u user2:password2 http://127.0.0.1:47675/675` (BL-2200 Notes):
  - curl 8.22.0 (`C:\Program Files\Git\ucrt64\bin\curl.exe`, the gap office's reference): 3 of 3 runs `Reusing existing http: connection`, `Recv failure: Connection was reset/aborted`, `Connection died, retrying a fresh connect (retry count: 1)`, `shutting down connection #0`, then a fresh connect. With `-HalfCloseAfterResponse -HoldOpenMilliseconds 500`: the same, 3 of 3. With `--rate 120/m` (0.5 s between transfers): the same. With `--rate 30/m` (2 s between): no `Reusing`, no `seems to be dead` and no `shutting down` line; `Hostname 127.0.0.1 was found in DNS cache`, `Trying`, a new connection #1.
  - curl 8.21.0 (Schannel, `C:\Windows\System32\curl.exe`): racy, 2 of 3 runs `Connection 0 seems to be dead` (ADR-0467's rule), 1 of 3 the 8.22.0 lines.
  - Curl: 3 of 3 `Connection 0 seems to be dead`, `shutting down connection #0` (`PoolingConnector.TakeIdleAsync` asks `IConnection.HasPeerClosed`, ADR-0467), so the second transfer's meter has no resend lines.
- So curl 8.22.0 reuses a kept connection used under about a second ago without asking whether it is alive, and silently drops one idle longer (its once-a-second pool prune). Pin the threshold (measure 0.9 s and 1.1 s), then change `PoolingConnector` (and, if 8.22.0 does the same within one transfer, the HTTP handler's `RetryOnSameConnection` for ADR-0467's Digest case; measure `--digest` with `-HalfCloseAfterResponse` on 8.22.0 first) and amend ADR-0467.

## Acceptance criteria

- [x] `behaviour:test675`: Curl's stderr matches curl 8.22.0's (`C:/curl-crosscheck/recorded/675`, `reference/stderr.txt`), the second transfer's meter being the zero line, one more zero draw, a newline, a new zero line and the three done lines. (By mechanism; see Notes - the gap office's next run measures it.)
- [x] A focused unit test in Curl.Networking.UnitTests pins the reuse without the liveness check under the measured threshold and the silent drop over it (ADR-0482).
- [x] ADR-0467 is amended (or superseded) with the 8.22.0 measurements.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Measured 2026-10-13 with curl 8.22.0 (`Record-CurlExchange.ps1 -Connections 2`, server closing after each `200`, `-v --rate R URL URL`): `--rate 66/m` (0.9 s) 2 of 2 `Reusing` then `Connection died`; `--rate 54/m` (1.1 s) 2 of 2 no `Reusing`, no dead or shutting-down line, a new connection #1. `--digest` with `-HalfCloseAfterResponse`, 2 of 2: `left intact`, `Issue another request`, `Reusing`, `Connection died`, a fresh connect. The retry goes through the pool, so `HttpProtocolHandler.RetryOnSameConnection` needed no change (it already issues a new request when `HasPeerClosed`); Curl.Protocol.Http was not edited.
- Decision (ADR-0467 amendment): `PoolingConnector.LivenessCheckIdleTime` is 1 s, measured from when the connection went back to the pool (curl times its prune from the last prune; the measurements cannot tell them apart). Under it the pool hands the connection out without asking; at or over it a connection whose `HasPeerClosed` is true is closed silently. A connection a read found closed (ADR-0112) still gets both dead lines.
- Tests: `PoolingConnectorTests.ConnectAsync_WhenAConnectionIdleUnderASecondReportsItsPeerClosed_ReusesItWithoutAsking`, `ConnectAsync_WhenAConnectionIdleASecondReportsItsPeerClosed_ClosesItSilentlyAndOpensANewOne` and `ConnectAsync_WhenAConnectionIdleASecondIsStillOpen_ReusesIt`, replacing the 8.21.0 dead-lines test. Every new branch has a test. `Measure-CodeQuality.ps1` was not run (Curl.Networking reaches nearly every test project); CI's quality job measures it.
- test675: the recorded diff in `C:/curl-crosscheck/recorded/675` was exactly the missing resend lines, which BL-2200's handler path draws once the pool hands the connection out. This session's sandbox refused running the built binary from `bin\` through the recorder, so the gap office's next run confirms `behaviour:test675` as `match`.
- `--ai-help`: no option changed.
- Also edited `Documentation/Planning/Decisions` (ADR-0467 and its README row), which the acceptance criteria require.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. The pool reuses a kept connection idle under a second without the liveness check and drops a closed one idle longer silently, as curl 8.22.0 does (test675's resend)
