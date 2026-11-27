---
id: BL-6071
title: Print one Connection timed out after N in both lines when -m ends an HTTP connect, redirect hops included
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6039]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Original.Protocol.Http.UnitTests/HttpConnectTimedOutLineTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6071 — Print one Connection timed out after N in both lines when -m ends an HTTP connect, redirect hops included

## Goal

An HTTP connect `-m` ends, a redirect hop's included, prints `Connection timed out after N milliseconds` as a `-v` line and in `curl: (28)` with the same N, never below the `-m` limit, as curl does.

## Context

Found in BL-6039 (ADR-1457). With a linux-x64 publish in WSL, `curl -sSv -m 3 http://192.0.2.20:1/` printed `* Connection timed out after 2855 milliseconds` from `TcpConnector` and `curl: (28) Connection timed out after 2942 milliseconds` from `HttpTransferDeadline.WithinConnectTimeoutAsync`, which counts its own N after the connect unwound and can print one below the limit. A redirect hop that starts well before `-m` runs out gets no `-v` line at all, on every platform: the connector's limit is the whole `-m` again (ADR-0117, BL-510 amendment), so the cancellation reaches it early and escapes. Measure real curl for the hop with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [x] A unit test pins that the failure's message carries the connector's N when the connector returned the timed-out result.
- [x] A unit test pins that a connect ended by `-m` that the connector did not report gets the `-v` line once, with the same N as the failure.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-1459): the deadline returns the connector's exit 28 result as it is; otherwise it counts N from the start of the request, reports the `-v` line itself, and stays silent for a connect abandoned by a race.
- Measured, curl 8.22.0 and 8.23.0 on Windows: on a redirect hop N counts from the hop's start, so it is below `-m`; "never below the `-m` limit" holds for the first request, where the connector's N is used (ADR-1457). curl also ends the hop at its next one-second poll tick, past `-m` when the redirect came early; Curl keeps ending it at `-m` (ADR-1459).
- Seen, not fixed here (needs the connector): the hop ends `closing connection #0` where curl prints `#1`, and a resolve `-m` ends on a hop says `Connection timed out`. Filed as BL-6074.
- Verified with the built `curl.exe`: the hop and a first connect to `192.0.2.20` each print the line once with the N of `curl: (28)`. Measure-CodeQuality for the HTTP library: 100% line and branch, 0 failing.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: ef3bad98 b19c3c7d 3d836d5e.
- 2026-11-26: Doing -> Done. An HTTP connect -m or the connect timeout ends prints one Connection timed out after N line with the N of curl: (28) (ADR-1459); hop connection number filed as BL-6074.
