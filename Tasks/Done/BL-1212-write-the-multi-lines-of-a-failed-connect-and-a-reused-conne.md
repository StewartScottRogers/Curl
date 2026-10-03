---
id: BL-1212
title: Write the [MULTI] lines of a failed connect and a reused connection for --trace-config multi
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1188]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1212 — Write the [MULTI] lines of a failed connect and a reused connection for --trace-config multi

## Goal

A refused connect and a transfer on a reused connection write curl 8.21.0's `[MULTI]` lines under `-v --trace-config multi`, as a plain HTTP transfer already does (BL-1188).

## Context

- BL-1188 (ADR-0382) added `Curl.Console`'s `MultiStateTraceEvents` for a plain HTTP transfer on a new connection. BL-1188 Notes hold the measured refused connect (`http://127.0.0.1:1/`): after `Trying`, the poll lines repeat, then `Curl_multi_will_close fd=N`, the `connect to ... failed` and `Failed to connect` lines, `failed to connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]`, `connect failed -> 7`, `multi_done: status: 7 prem: 1 done: 0`, `multi_done_locked, in use=0`, `multi_done, terminating conn #0 to 127.0.0.1:1, forbid=0, close=0, premature=1, conn_multiplex=0`, `closing connection #0`, `-> [COMPLETED]`, three `[COMPLETED] [PGRS-*] added` lines (PRETRANSFER, POSTRANSFER, STARTTRANSFER), `-> [MSGSENT]`, `removed from multi`. Today such a transfer writes the connect groups and no closing lines.
- A reused connection writes none of the connect groups today but still writes `reduced to [0][TCP]`, `-> [PROTOCONNECT]` and `[PROTOCONNECT] -> [DO]`, which curl does not write for a reused connection. Measure first: `Record-CurlExchange.ps1 -Connections 1` with two URLs and a keep-alive response (the BL-1188 two-URL measurement had its connection reset, so measure again).

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1` (refused connect, two URLs on one kept-alive connection); stderr in Notes.
- [x] `MultiStateTraceEventsTests` pin the refused connect's and the reused connection's `[MULTI]` lines in curl's order.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -s -v --trace-config multi`, fixtures in `%TEMP%\bl1212\<case>`:
  - `refused` (`http://127.0.0.1:1/`, exit 7), after the `[INIT]`..`cf_setup_connect [...][!HAPPY-EYEBALLS]` lines as BL-1188 measured: `Trying 127.0.0.1:1...`, then twice `pollset[fd=428 OUT], timeouts=0`, `multi_wait(fds=1, timeout=1000) tinternal=-1`, `cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]`; `[CONNECTING] Curl_multi_will_close fd=428`, `connect to 127.0.0.1 port 1 from 0.0.0.0 port 60537 failed: Connection refused`, `Failed to connect to 127.0.0.1:1 after 2024 ms: Could not connect to server`, `[CONNECTING] failed to connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]`, `[CONNECTING] connect failed -> 7`, `[CONNECTING] multi_done: status: 7 prem: 1 done: 0`, `[CONNECTING] multi_done_locked, in use=0`, `[CONNECTING] multi_done, terminating conn #0 to 127.0.0.1:1, forbid=0, close=0, premature=1, conn_multiplex=0`, `closing connection #0`, `[CONNECTING] -> [COMPLETED]`, `[COMPLETED] [PGRS-PRETRANSFER] added 2024209ns`, `[PGRS-POSTRANSFER] added 2024215ns`, `[PGRS-STARTTRANSFER] added 2024220ns`, `[COMPLETED] -> [MSGSENT]`, `[COMPLETED] removed from multi, mid=1, running=0, total=1`.
  - `reused` (two URLs, `-HoldOpenMilliseconds 3000 -AnswerHeldRequests 1`, `200` with `Content-Length: 2`): the first transfer as BL-1188's `plain`; the second: the seven start lines, `[CONNECT] transfer credentials: -`, `Reusing existing http: connection with host 127.0.0.1`, `[CONNECT] [PGRS-POSTQUEUE] set`, `[CONNECT] -> [CONNECTING]`, `[CONNECTING] -> [PROTOCONNECT]`, `[PROTOCONNECT] -> [DO]`, `[DO] xfer_setup: recv_idx=0, send_idx=0`, request, `Request completely sent off`, then the same lines as a new connection (`[PGRS-*] added 229/232/4002ns`) to `removed from multi`. No `[CPOOL]`, `Curl_conn_setup`, `reduced to` or `using HTTP/1.x`.
- Before this task Curl wrote nothing of curl's after the connect groups for the refused connect; for the reused one, no `transfer credentials`/`POSTQUEUE`/`-> [CONNECTING]` lines, and `reduced to [0][TCP]` .. `xfer_setup` after the request header.
- Delivered in `Curl.Console`'s `MultiStateTraceEvents` (ADR-0391): `ReportConnectionReused` writes the reused lines around the event and goes on from the request-sent group; a `connect to ... failed` line gets the poll lines (once) and `Curl_multi_will_close fd=<descriptor>` before it; the `Failed to connect to` line switches to the failed connect's two groups (after it, and after `closing connection #`), naming the connection number the `[CPOOL]` line took and the line's host:port. Run against the built `curl.exe`, both cases now match curl's lines but for curl's second poll round during Windows' 2 s refusal.
- Decision (ADR-0391): the poll lines are written once, as their count follows the platform's SYN retries, which Curl does not see. A connect that times out (exit 28) has no `Failed to connect to` line and so writes no failed-connect groups; outside this task's measured cases.
- Tests: `MultiStateTraceEventsTests.TheMeasuredReusedConnection_*` (replaces `AReusedConnection_WritesNoConnectGroupAndTakesNoConnectionNumber`), `TheMeasuredRefusedConnect_*`, `AFailedAddressBeforeOneThatConnects_*`, `ASecondFailedToConnectLine_*`. `dotnet build Curl.slnx -warnaserror` clean; fast tests 24961 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members.
- Also moved ADR-0390's row in the Decisions README from after the template block into the table, beside ADR-0391's.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A refused connect and a reused connection write curl 8.21.0's [MULTI] lines under --trace-config multi
