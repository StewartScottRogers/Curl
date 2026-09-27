---
id: BL-402
title: Report Trying, connection opened and connect failure events from TcpConnector
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-402 — Report Trying, connection opened and connect failure events from TcpConnector

## Goal

The TCP connector reports curl's `  Trying <ip>:<port>...` info line, `ReportConnectionOpened` once connected (after the TLS handshake on HTTPS), and curl's connect-failure lines to `ConnectTarget.Events`.

## Context

- ADR-0046, "The connector reports through `ConnectTarget`". BL-228 Notes: on HTTPS the `Established connection` line comes after the ALPN lines.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): a success printed `*   Trying 127.0.0.1:18441...` then `* Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 ` (trailing space). A refused connection, `curl -v -s http://127.0.0.1:1/`, printed `*   Trying 127.0.0.1:1...`, `* connect to 127.0.0.1 port 1 from 0.0.0.0 port 56585 failed: Connection refused`, `* Failed to connect to 127.0.0.1:1 after 2025 ms: Could not connect to server` and `* closing connection #0`.

## Acceptance criteria

- [ ] A test over a fake dialer records `  Trying 127.0.0.1:18441...` and then a `ConnectionOpenedEvent` carrying the remote and local end points and the connection number.
- [ ] A refused connect records the measured failure lines, the elapsed milliseconds taken from the injected `TimeProvider`.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.

## Log

- 2026-09-27: Created.
