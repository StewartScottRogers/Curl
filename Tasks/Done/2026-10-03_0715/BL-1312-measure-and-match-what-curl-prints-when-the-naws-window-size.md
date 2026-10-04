---
id: BL-1312
title: Measure and match what curl prints when the NAWS window size write of a telnet subnegotiation fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1307]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1312 — Measure and match what curl prints when the NAWS window size write of a telnet subnegotiation fails

## Goal

When the 4-byte window size write inside a telnet NAWS subnegotiation fails, Curl writes exactly the `-v` line(s) curl 8.21.0 writes for it (measured), not just the `Sending data failed (N)` lines for the header and footer writes.

## Context

- curl 8.21.0 `lib/telnet.c` `sendsuboption` (lines 671-716): the header (`IAC SB NAWS`) and footer (`IAC SE`) are `swrite`s whose failure is `failf(data, "Sending data failed (%d)", SOCKERRNO)`; the window size between them goes through `send_telnet_data`, whose result is ignored. That path runs `Curl_poll` then `Curl_xfer_send`, and the socket filter's send may itself `failf` (`Send failure: <strerror>`), which would print a line of its own text, which differs by platform.
- BL-1307 made that middle write silent on failure (`TelnetOutbox.SendUnreported`, called from `TelnetReceiver.AppendWindowSize`), because the line was not measured; pinned by `TelnetProtocolHandlerSendFailureTests.ExecuteAsync_WindowSizeSubnegotiationWritesFail_ReportsFailuresAfterTheSuboptionAndGoesOn`.
- Measure with `Record-CurlExchange.ps1 -Response '\xff\xfd\x1f' ...` and `-sv -t WS=80x24 telnet://127.0.0.1:PORT` against a server that closes right after sending, on Windows (Schannel build); record Linux and macOS text too if available.

## Acceptance criteria

- [x] The measured stderr of the run above is in this task's Notes.
- [x] `TelnetProtocolHandlerSendFailureTests` pins the lines Curl writes when the window size write fails, matching the measurement (per platform where they differ).
- [x] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary` reports no failing member.

## Notes

- 2026-10-03 (lane 7): Measured curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '\xff\xfd\x1f' -CurlArgs '-sv,-t,WS=80x24,telnet://127.0.0.1:PORT'`, three runs: exit 0, every write taken, stderr
  ```
  *   Trying 127.0.0.1:47312...
  * Established connection to 127.0.0.1 (127.0.0.1 port 47312) from 127.0.0.1 port 51911
  * RCVD DO NAWS
  * SENT WILL NAWS
  * SENT IAC SB
  * NAWS
  * Width: 80 ; Height: 24
  * SENT WILL BINARY
  * SENT DO BINARY
  * SENT WILL SUPPRESS GO AHEAD
  * SENT DO SUPPRESS GO AHEAD
  * shutting down connection #0
  ```
  (with `-HoldOpenMilliseconds` the server received `FFFB1F FFFA1F 00500018 FFF0 ...`: a window size write that succeeds writes no `-v` line, as Curl already does).
- No set-up made the window size write fail; each gave the same lines with no failure at all: closing with an RST (new recorder switch `-ResetAfterResponse`), `DO TERM-TYPE` first so a first write meets a closed peer (BL-1307's set-up, which did fail on 2026-10-02 but no longer loses the race), 2 KB / 20 KB / 200 KB of filler before `DO NAWS` with FIN or RST, and `--limit-rate 2000` (telnet ignores it). curl writes everything before the peer's close takes effect on loopback.
- Decision (ADR-0403, decided under Stewart's delegation): follow curl's source. The window size goes through `send_telnet_data` -> `Curl_xfer_send` -> `cf_socket_send`, whose send error is `failf(data, "Send failure: %s", curlx_strerror(...))`. Curl now writes `Send failure: <text>` between the header's and footer's `Sending data failed (N)`: Windows uses the Schannel build's Winsock words (`Connection was reset`/`Connection was aborted`, ADR-0088), elsewhere the error's own message (.NET's `strerror`). New `TelnetSocketErrorText`; `TelnetOutbox.SendUnreported` became `SendTelnetData`; `SendReplyAsync` now hands back the `SocketException`.
- Tests: two per-platform pins in `TelnetProtocolHandlerSendFailureTests` (Windows `Send failure: Connection was aborted`, elsewhere `Send failure: Software caused connection abort`), the existing full transcript updated, `TelnetSocketErrorTextTests` (5). Added `InternalsVisibleTo` for the telnet tests, as other libraries do. Telnet suite 230 passed, 1 skipped (the off-Windows pin); solution fast tests green.
- Quality: telnet tests' own Cobertura run and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary -SkipTestRun -ResultsDirectory`: 0 failing members.
- Touches: added `Record-CurlExchange.ps1` for the `-ResetAfterResponse` switch; no task in Doing on `origin/work/dark-factory` named it.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A failed NAWS window size write reports curl's Send failure: <text> line between the suboption's Sending data failed lines (ADR-0403)
