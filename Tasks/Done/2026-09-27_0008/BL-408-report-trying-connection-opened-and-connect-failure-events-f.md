---
id: BL-408
title: Report Trying, connection opened and connect failure events from TcpConnector
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-408 — Report Trying, connection opened and connect failure events from TcpConnector

## Goal

The TCP connector reports curl's `  Trying <ip>:<port>...` info line, `ReportConnectionOpened` once connected (after the TLS handshake on HTTPS), and curl's connect-failure lines to `ConnectTarget.Events`.

## Context

- ADR-0046, "The connector reports through `ConnectTarget`". BL-228 Notes: on HTTPS the `Established connection` line comes after the ALPN lines.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): a success printed `*   Trying 127.0.0.1:18441...` then `* Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 ` (trailing space). A refused connection, `curl -v -s http://127.0.0.1:1/`, printed `*   Trying 127.0.0.1:1...`, `* connect to 127.0.0.1 port 1 from 0.0.0.0 port 56585 failed: Connection refused`, `* Failed to connect to 127.0.0.1:1 after 2025 ms: Could not connect to server` and `* closing connection #0`.

## Acceptance criteria

- [x] A test over a fake dialer records `  Trying 127.0.0.1:18441...` and then a `ConnectionOpenedEvent` carrying the remote and local end points and the connection number.
- [x] A refused connect records the measured failure lines, the elapsed milliseconds taken from the injected `TimeProvider`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.
- Delivered by the lane directly (plan small enough for one library): `TcpConnector` reports
  `Trying` per dial, `connect to ... from 0.0.0.0 port 0 failed: <reason>` per failed dial, the
  exit 7 message, and `ReportConnectionOpened` after any tunnel and TLS handshake; it numbers its
  connections from 0. New `ConnectFailureReason` words the reason (curl's Winsock words on
  Windows, the system message elsewhere). Decisions in ADR-0100.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0100; no task in Doing names it.
- The local port in the failure line is `0`: `ITcpDialer` reports a failed dial as a bare
  `SocketException`, and curl's port is ephemeral anyway.
- `closing connection #0` after a failed connect is the handler's line, not the connector's:
  filed as BL-453.
- Tests: 13 new (8 in `TcpConnectorTests.Events.cs`, 5 methods in `ConnectFailureReasonTests`);
  Networking 717 passed, 6 skipped; whole fast run green. Coverage 100% line, 100% branch,
  worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v shows curl's Trying, connect-failure and Established connection lines from TcpConnector
