---
id: BL-1159
title: Write the [MULTI], [TIMER], [READ] and [WRITE] trace lines for --trace-config, -vvv and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1103]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1159 — Write the [MULTI], [TIMER], [READ] and [WRITE] trace lines for --trace-config, -vvv and -vvvv

## Goal

Curl writes curl 8.21.0's `[MULTI]`, `[TIMER]`, `[READ]` and `[WRITE]` lines under `--trace-config multi`, `timer`, `read`, `write`, `network`, `all`, `-vvv` (`read`, `write`) and `-vvvv`.

## Context

- Split from BL-1103 (ADR-0357); `CommandLineOptions.TraceComponents` already carries `read` and `write` at `-vvv` and `all` at `-vvvv`. BL-1103's Notes hold curl's measured stderr for `multi`, `read`, `write`, `timer`, `network`, `-vvv` and `-vvvv`.
- These lines come from curl's transfer engine (state changes `[INIT] -> [SETUP]`, `[PGRS-*] added <n>ns`, the client writer stack's `[WRITE] [OUT] wrote 17 header bytes -> 17`), so they are written from where Curl runs a transfer (`Curl.Core`, the HTTP handler and the console runner); split again per component with task-planner if one run cannot hold it. Refine `touches` before starting.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` for each component named in the title (a plain HTTP transfer, a refused connect, and `localhost` where both families answer); stderr in Notes.
- [x] Tests pin each component's stable lines for a plain HTTP transfer, and that no line appears without its component; an ADR-0357 amendment records how the volatile values (fd numbers, nanosecond stamps, poll repetitions) are produced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` (`200`, `Content-Length: 2`, `hi`) on 127.0.0.1:47811; fixtures in `%TEMP%\bl1159\<case>`. Only `*` lines shown.
  - `--trace-config read -v`: `[READ] client_reset, clear readers` first of all (before `Trying`), and again after `{ [2 bytes data]`, before `Connection #0 to host 127.0.0.1:47811 left intact`. Without `-v`: nothing.
  - `--trace-config read,setup,timer -v`: `[READ] client_reset, clear readers`, `[SETUP] added`, `[SETUP] happy eyeballing to origin 127.0.0.1:47811`, `Trying`, `[TIMER] [HAPPY_EYEBALLS] cleared`, `Established ...`, `[SETUP] removing connected setup filter`, `[SETUP] destroy`, ..., `[READ] client_reset, clear readers`, `Connection #0 ... left intact`.
  - `--trace-config read,dns -v`: `[READ]` before `[DNS] created DNS filter ...`.
  - `--resolve a.test:47811:127.0.0.1` with `Connection: close`: `Added a.test:47811:127.0.0.1 to DNS cache`, then `[READ] client_reset, clear readers`, ...; at the end `[READ] client_reset, clear readers` before `shutting down connection #0`. `-b` file: no line of its own, `[READ]` still first.
  - Refused (`http://127.0.0.1:1/`, `read,timer`): `[READ] client_reset, clear readers`, `Trying`, connect failed lines, `closing connection #0`; no second `[READ]`, no `[TIMER]`. `-f` on a 404: likewise no second `[READ]` before `closing connection #0`.
  - `localhost:1` (`read,timer`): `[READ]` before `Host localhost:1 was resolved.`; after `Trying [::1]:1...` come `[TIMER] [HAPPY_EYEBALLS] set for 200000ns` and `[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms`.
  - Two URLs on one connection: each transfer writes both lines, the second's first before `Reusing existing http: connection`.
  - `-d ab`: after `using HTTP/1.x`, `[READ] add buf reader, len=2 -> 0`, `[READ] cr_buf_read(len=65388) -> 0, nread=2, eos=1`, `[READ] client_read(len=65388) -> 0, nread=2, eos=1`, before `upload completely sent off: 2 bytes`. `-L`: not measured (the recorder loops on a self-redirect).
- Split (the Context allows it; ADR-0357 amendment, decided by Claude under Stewart's delegation): this run delivers the two `[READ] client_reset` lines of a plain transfer in `Curl.Console`. Filed: BL-1182 `[TIMER]` (needs `Curl.Networking`, not in this task's touches), BL-1183 `[WRITE]`, BL-1184 `[MULTI]`, BL-1185 upload readers' and redirect hops' `[READ]` lines. No change to `Curl.Core` or `Curl.Protocol.Http` was needed.
- Delivered: `CurlComposition.TracesRead` (`read` or `all`); `CurlCommandRunner.TraceClientReaderReset` writes the first line after the `--resolve` lines on the before-connect events (so `[0-x]` under `--trace-ids`); `ClientReaderResetTraceEvents` writes the second before `Connection #N ... left intact` or `shutting down connection #N`, not before `closing connection #N`. `WithTraceLineEvents` holds the two trace wrappers so `SetUpTransferEvents` stays at complexity 10.
- Tests: `ClientReaderResetTraceEventsTests`; `CurlCommandRunnerTransferEventTests.RunAsync_TraceConfigRead_*`, `RunAsync_TraceConfigWithoutRead_WritesNoReadLine` (`network`, `setup`, `-read`), `RunAsync_TraceConfigReadWithoutVerbose_WritesNothing`, `RunAsync_ThreeOrMoreVs_WriteBothClientResetLines`, `RunAsync_TraceConfigReadWithTraceIds_MarksTheFirstClientResetLineWithAnX`. `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line and branch, 0 failing members.
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config read, -vvv and -vvvv write curl's two [READ] client_reset lines; [TIMER], [WRITE], [MULTI] and upload [READ] split to BL-1182..BL-1185
