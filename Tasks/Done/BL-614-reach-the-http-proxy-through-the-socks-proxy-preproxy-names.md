---
id: BL-614
title: Reach the HTTP proxy through the SOCKS proxy --preproxy names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-614 — Reach the HTTP proxy through the SOCKS proxy --preproxy names

## Goal

With `--preproxy socks5://...` and `-x http://...`, the connector reaches the HTTP proxy through the SOCKS proxy (SOCKS handshake to the HTTP proxy's address, then the HTTP proxy's `CONNECT` or forwarded request), as curl 8.21.0 does, with each hop's failure mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612.
- Code: `Curl.Networking.UnitLibrary/SocksProxyTunnel.cs`, `Socks4Handshake.cs`, `Socks5Handshake.cs` (ADR-0084), `HttpProxyTunnel.cs`, `TcpConnector.cs`; proxy selection in `Curl.Core.UnitLibrary/ProxySelector.cs` and `Curl.Console/TransferProxySelection.cs`. The pool key must include the pre-proxy.
- `Record-CurlExchange.ps1` serves HTTP only; a SOCKS5 no-auth handshake is a fixed byte exchange, so measure with a scripted exchange (BL-532's `-Script` mode if it has landed, otherwise extend the script) and record the bytes.

## Acceptance criteria

- [x] Measured first as above: `--preproxy socks5://127.0.0.1:<S> -x http://10.0.0.1:<P> http://h/` (request bytes on the SOCKS side), and the SOCKS proxy refusing; stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the chained handshake bytes and each hop's failure through fake dialers.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-30 against curl 8.21.0 (Schannel, `/mingw64/bin/curl`) with
`Record-CurlExchange.ps1 -Port 41080 -Script <file>` playing a SOCKS5 server (steps `read 3`,
`send \x05\x00`, `read`, `send <reply>`, ...):

- `-sS -v --preproxy socks5://127.0.0.1:41080 -x http://10.0.0.1:3128 http://h/`, reply `05 00 00 01 7f 00 00 01 1f 90`
  then `HTTP/1.1 200 OK` with `hi`: the SOCKS side got `05 02 00 01` (no auth and GSSAPI), `05 01 00 01 0a 00 00 01 0c 38`
  (connect to 10.0.0.1:3128), then `GET http://h/ HTTP/1.1`, `Host: h`, `User-Agent: curl/8.21.0`, `Accept: */*`,
  `Proxy-Connection: Keep-Alive`. Exit 0, stdout `hi`. `-v`: `* Opened SOCKS connection from 127.0.0.1 port N to 10.0.0.1
  port 3128 (via 127.0.0.1 port 41080)` and `* Connection #0 to host 10.0.0.1:3128 left intact`.
- The same with `https://h/` and a `403` to the CONNECT: `CONNECT h:443` goes inside the SOCKS tunnel;
  `curl: (7) CONNECT tunnel failed, response 403`, exit 7.
- The SOCKS proxy refusing (reply `05 05 00 01 ...`): `curl: (97) cannot complete SOCKS5 connection to 10.0.0.1. (5)`, exit 97.
- The SOCKS proxy not listening: `curl: (7) Failed to connect to 10.0.0.1:3128 over proxy 127.0.0.1 after 2048 ms: Could not connect to server`, exit 7.
- The pre-proxy unresolvable: `curl: (5) Could not resolve proxy: nonexistent.invalid`, exit 5.
- The HTTP proxy's name unresolvable through socks5 (local resolve): `curl: (6) Could not resolve host: nonexistent.invalid`, exit 6.
- `--preproxy` alone: the pre-proxy is the SOCKS proxy for the URL's host (`Could not resolve host: h`, exit 6); the environment is not read.
- No scheme: SOCKS4 (`04 01 0c 38 0a 00 00 01 00` sent).
- `--preproxy socks5://... -x socks5://127.0.0.1:41099`: `curl: (5) Having a SOCKS pre-proxy and proxy is not supported with 'socks5://127.0.0.1:41099'`
  (`--socks5 127.0.0.1:41099` quotes `'127.0.0.1:41099'`).
- `--preproxy http://...` or `https://...`: `curl: (5) Unsupported pre-proxy type for '<text>'`, checked before `-x`
  (`--preproxy "bad host" -x "also bad"` reports `'bad host'`).
- `--noproxy h` drops both proxies (`Could not resolve host: h`, exit 6).
- `--preproxy ""`: exit 2, blank argument (the parser's, BL-612).

Decisions (ADR-0272, decided by Claude under Stewart's delegation): the pre-proxy is a `TcpConnector`
constructor argument per option group (`CurlComposition.PreProxyOf`), used only on the way to an HTTP or HTTPS
proxy or a forward proxy, so neither `ConnectTarget` nor the pool key changes (each group has its own pool, and
all of it goes through the same pre-proxy). `TransferProxySelection` makes the per-transfer refusals and turns a
lone pre-proxy into the SOCKS `ConnectTarget.Proxy`. `-U` applies to a lone pre-proxy, as libcurl copies the
proxy user to the SOCKS proxy.

`touches` gained `Documentation/Planning/Decisions` for ADR-0272 and its index row; no task in Doing names it
(BL-986 touches only the HTTP projects).

Our build replays the measured success, CONNECT and refusal exchanges byte for byte (identical request.bin,
same stderr and exit codes). Not done here, for every SOCKS tunnel: curl's `-v` line `Opened SOCKS connection
... (via ...)` is not printed yet; filed as a follow-up.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --preproxy reaches the HTTP proxy through the SOCKS proxy, byte for byte as curl 8.21.0, with each hop's failure mapped
