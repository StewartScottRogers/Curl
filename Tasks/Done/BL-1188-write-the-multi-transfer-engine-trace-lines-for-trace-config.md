---
id: BL-1188
title: Write the [MULTI] transfer engine trace lines for --trace-config multi, network and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1188 — Write the [MULTI] transfer engine trace lines for --trace-config multi, network and -vvvv

## Goal

Curl writes curl 8.21.0's `[MULTI]` lines under `--trace-config multi`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1159 (ADR-0357 amendment). BL-1103 Notes hold the measured lines: `[MULTI] [INIT] added to multi, mid=1, running=1, total=2`, `pollset[]`, `multi_wait(...)`, state changes `[INIT] -> [SETUP]` ... `[COMPLETED] -> [MSGSENT]`, `[PGRS-*] set|added <n>ns`, `[CPOOL] added connection 0. The cache now contains 1 members`, `cf_setup_connect`, `xfer_setup: recv_idx=0, send_idx=0`, `multi_done: status: 0 prem: 0 done: 0`, `removed from multi, mid=1, running=0, total=1`. These come from curl's multi state machine, which Curl does not have as such: decide (ADR) which states Curl reports and where, and how the nanosecond stamps and poll lines are produced. Split again per line family with task-planner if one run cannot hold it.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (plain HTTP transfer, refused connect); stderr in Notes.
- [x] Tests pin the stable `[MULTI]` lines of a plain HTTP transfer, and that none appears without `multi`, `network` or `all`; an ADR records how the volatile values are produced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, fixtures in `%TEMP%\bl1188\<case>`:
  - `plain` (`200`, `Content-Length: 2`, `hi`, `-s -v --trace-config multi`): `[INIT] added to multi, mid=1, running=1, total=2`, `[INIT] pollset[], timeouts=0, paused 0/0 (r/w)`, `[INIT] multi_wait(fds=0, timeout=0) tinternal=0`, `[INIT] -> [SETUP]`, `[SETUP] [PGRS-STARTOP] set`, `[SETUP] [PGRS-STARTSINGLE] set`, `[SETUP] -> [CONNECT]`, `[CONNECT] transfer credentials: -`, `[CONNECT] [CPOOL] added connection 0. The cache now contains 1 members`, `[CONNECT] [PGRS-POSTQUEUE] set`, `[CONNECT] Curl_conn_setup() -> 0`, `[CONNECT] -> [CONNECTING]`, `[CONNECTING] [PGRS-NAMELOOKUP] added 59ns`, `cf_setup_connect [0][!DNS][!SETUP]`, `cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]`, Trying, `pollset[fd=424 OUT], timeouts=0`, `multi_wait(fds=1, timeout=1000) tinternal=-1`, `cf_setup_connect [...][!HAPPY-EYEBALLS]`, `[PGRS-CONNECT] added 633ns`, Established, `connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]`, `reduced to [0][TCP]`, `-> [PROTOCONNECT]`, `[PROTOCONNECT] -> [DO]`, `using HTTP/1.x`, `[DO] xfer_setup: recv_idx=0, send_idx=0`, request, `Request completely sent off`, `[DO] -> [DID]`, `[DID] [PGRS-PRETRANSFER] added 732ns`, `[PGRS-POSTRANSFER] added 735ns`, `[DID] -> [PERFORMING]`, `[PERFORMING] pollset[fd=424 IN], timeouts=0`, `multi_wait(...)`, status line, `[PERFORMING] [PGRS-STARTTRANSFER] added 30768ns`, rest of head, data, `[PERFORMING] -> [DONE]`, `[DONE] multi_done: status: 0 prem: 0 done: 0`, `[DONE] multi_done_locked, in use=0`, `Connection #0 ... left intact`, `[DONE] -> [COMPLETED]`, `[COMPLETED] -> [MSGSENT]`, `[COMPLETED] removed from multi, mid=1, running=0, total=1`. The `ns` numbers are microseconds (30768 for a ~30 ms response).
  - `refused` (`http://127.0.0.1:1/`, exit 7): the same to `Trying`; then `pollset[fd=440 OUT]`, `multi_wait`, `cf_setup_connect` twice more (2 s of retries), `Curl_multi_will_close fd=440`, `connect to 127.0.0.1 port 1 ... failed: Connection refused`, `Failed to connect ...`, `failed to connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]`, `connect failed -> 7`, `multi_done: status: 7 prem: 1 done: 0`, `multi_done_locked, in use=0`, `multi_done, terminating conn #0 to 127.0.0.1:1, forbid=0, close=0, premature=1, conn_multiplex=0`, `closing connection #0`, `[CONNECTING] -> [COMPLETED]`, `[COMPLETED] [PGRS-PRETRANSFER|POSTRANSFER|STARTTRANSFER] added 20177xxns`, `-> [MSGSENT]`, `removed from multi`.
  - `all`: the interleaving pinned in `MultiStateTraceEventsTests.TheMeasuredPlainTransferUnderAll_*`; `[READ] client_reset` comes between `[SETUP] -> [CONNECT]` and `transfer credentials`; `multi_done` before `[WRITE] [OUT] done` and `[READ]`, `multi_done_locked` after (with `[0-x]` under ids).
  - `two` (two URLs): each transfer writes its own `[INIT] added to multi, mid=1` ... `removed from multi, mid=1`; the second connection's `[CPOOL] added connection 1`.
- Delivered in `Curl.Console` only: `MultiStateTraceEvents` (inside the `[READ]`/`[WRITE]` events in `CurlCommandRunner.WithTraceLineEvents`) under the new `CurlComposition.TracesMulti` (`multi`, `network`, `all`); the runner's `TraceTransferStart` (was `TraceClientReaderReset`) writes `StartLines` before `[READ] client_reset`. How groups are placed and the volatile values produced: ADR-0382.
- Scope: plain HTTP on a new connection. A failed connect and a reused connection's own lines are BL-1212 (filed). The `[PGRS-CONNECT]` group is tied to the connection opened event, since `Established connection` is written from it, not as an info line.
- `CurlCommandRunnerTransferEventTests.SettableClock` now also stands its timestamps still (it only faked wall time), so the runner tests' `[PGRS-*]` numbers are 0; `RunAsync_TraceConfigWithoutRead_WritesNoReadLine`'s `network` row became `dns`, since `network` now writes `[MULTI]` lines.
- Tests: `MultiStateTraceEventsTests` (multi alone, all components, reused, shutting down, unrelated lines and events); `CurlCommandRunnerTransferEventTests.RunAsync_TraceConfigMulti_*`, `RunAsync_TraceConfigAll_WritesTheMultiDoneLinesBeforeTheWriteAndReadLines`, `RunAsync_NetworkOrFourVs_WriteTheMultiLines`, `RunAsync_WithoutMultiNetworkOrAll_WritesNoMultiLine` (`read,write`, `-multi`, `-vvv`). `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line and branch; its one failing member, `CurlCommandRunner.TransferUrlAsync` (complexity 12 by the script, CA1502 passes), is unchanged by this task. `Curl.Core` and `Curl.Networking` were not changed.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config multi, network, all and -vvvv write curl's [MULTI] transfer engine lines for a plain HTTP transfer in curl's order; failed and reused connects are BL-1212
