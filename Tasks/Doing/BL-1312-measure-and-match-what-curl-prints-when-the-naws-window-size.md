---
id: BL-1312
title: Measure and match what curl prints when the NAWS window size write of a telnet subnegotiation fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1307]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1312 — Measure and match what curl prints when the NAWS window size write of a telnet subnegotiation fails

## Goal

When the 4-byte window size write inside a telnet NAWS subnegotiation fails, Curl writes exactly the `-v` line(s) curl 8.21.0 writes for it (measured), not just the `Sending data failed (N)` lines for the header and footer writes.

## Context

- curl 8.21.0 `lib/telnet.c` `sendsuboption` (lines 671-716): the header (`IAC SB NAWS`) and footer (`IAC SE`) are `swrite`s whose failure is `failf(data, "Sending data failed (%d)", SOCKERRNO)`; the window size between them goes through `send_telnet_data`, whose result is ignored. That path runs `Curl_poll` then `Curl_xfer_send`, and the socket filter's send may itself `failf` (`Send failure: <strerror>`), which would print a line of its own text, which differs by platform.
- BL-1307 made that middle write silent on failure (`TelnetOutbox.SendUnreported`, called from `TelnetReceiver.AppendWindowSize`), because the line was not measured; pinned by `TelnetProtocolHandlerSendFailureTests.ExecuteAsync_WindowSizeSubnegotiationWritesFail_ReportsFailuresAfterTheSuboptionAndGoesOn`.
- Measure with `Record-CurlExchange.ps1 -Response '\xff\xfd\x1f' ...` and `-sv -t WS=80x24 telnet://127.0.0.1:PORT` against a server that closes right after sending, on Windows (Schannel build); record Linux and macOS text too if available.

## Acceptance criteria

- [ ] The measured stderr of the run above is in this task's Notes.
- [ ] `TelnetProtocolHandlerSendFailureTests` pins the lines Curl writes when the window size write fails, matching the measurement (per platform where they differ).
- [ ] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
