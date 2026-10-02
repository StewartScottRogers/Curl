---
id: BL-1038
title: Print curl's Opened SOCKS connection -v line for every SOCKS tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1038 — Print curl's Opened SOCKS connection -v line for every SOCKS tunnel

## Goal

Under `-v`, every SOCKS tunnel `TcpConnector` opens (`-x socks*://`, `--socks*`, `--preproxy`) reports curl 8.21.0's `* Opened SOCKS connection from <local ip> port <local port> to <host> port <port> (via <proxy ip> port <proxy port>)` line once the handshake succeeds.

## Context

- Found in BL-614: `curl -sS -v --preproxy socks5://127.0.0.1:41080 -x http://10.0.0.1:3128 http://h/` prints
  `* Opened SOCKS connection from 127.0.0.1 port 64919 to 10.0.0.1 port 3128 (via 127.0.0.1 port 41080)` right after
  `*   Trying 127.0.0.1:41080...`; Curl prints nothing there (BL-614 Notes have the whole exchange).
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs` `OpenSocksHopAsync`, which knows the dialled `DialedSocket`'s end points; report through `ConnectTarget.Events.ReportInfo`.
- Measure first which host is named for SOCKS4a and SOCKS5h (the name as sent, or the address) with `Record-CurlExchange.ps1 -Script`.

## Acceptance criteria

- [ ] Measured first for SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h, and through `--preproxy`; the `-v` lines copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the line for each kind through fakes, and that nothing is reported for a failed handshake.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
