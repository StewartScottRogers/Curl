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
completed: 2026-10-02
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

- [x] Measured first for SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h, and through `--preproxy`; the `-v` lines copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the line for each kind through fakes, and that nothing is reported for a failed handshake.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

Measured 2026-10-02 against curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Port 41080 -Script`
playing a SOCKS4 (`read`, `send \x00\x5a...`) or SOCKS5 (`read`, `send \x05\x00`, `read`,
`send \x05\x00\x00\x01\x7f\x00\x00\x01\x1f\x90`) server, then an HTTP 200:

- `-x socks4://127.0.0.1:41080 --resolve h.test:80:10.0.0.1 http://h.test/`:
  `* Opened SOCKS connection from 127.0.0.1 port 60389 to h.test port 80 (via 127.0.0.1 port 41080)`
- `-x socks4a://... http://h.test/`: `... port 60390 to h.test port 80 (via 127.0.0.1 port 41080)`
- `-x socks5://... --resolve h.test:80:10.0.0.1 http://h.test/`: `... port 60392 to h.test port 80 (via 127.0.0.1 port 41080)`
- `-x socks5h://... http://h.test/`: `... port 60393 to h.test port 80 (via 127.0.0.1 port 41080)`
- `--socks5 127.0.0.1:41080 http://10.0.0.2:8080/`: `... to 10.0.0.2 port 8080 (via ...)`
- `--preproxy socks5://127.0.0.1:41080 -x http://10.0.0.1:3128 http://h/`: `... to 10.0.0.1 port 3128 (via 127.0.0.1 port 41080)`
- `--preproxy socks5h://... -x http://proxy.test:3128 http://h/`: `... to proxy.test port 3128 (via ...)`
- A refused SOCKS5 connect (`05 05 ...`): only `* cannot complete SOCKS5 connection to h.test. (5)`, no Opened line.

So every kind names the destination as given (never the address SOCKS4 or SOCKS5 sent), and the
proxy by the address dialled. The line comes right after the handshake, before `Established connection`.

Done in `TcpConnector.OpenSocksHopAsync`, which now takes the `DialedSocket` for its end points and
reports the line through `ConnectTarget.Events` once the handshake succeeds. A SOCKS hop is always
dialled over TCP, so both end points are known (null-forgiving rather than an unreachable branch).
Tests: `TcpConnectorTests.Socks.cs` (each kind, and a failed handshake) and
`TcpConnectorTests.PreProxy.cs` (a CONNECT and a forward proxy behind a pre-proxy).

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. curl -v prints curl's Opened SOCKS connection line for every SOCKS tunnel, --preproxy included
