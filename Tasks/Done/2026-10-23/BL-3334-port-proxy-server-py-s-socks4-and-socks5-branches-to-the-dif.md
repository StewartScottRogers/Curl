---
id: BL-3334
title: Port proxy_server.py's SOCKS4 and SOCKS5 branches to the differential runner's proxy responder
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Proxy, Curl.Conformance.Differential.UnitTests/Protocols/Proxy]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3334 — Port proxy_server.py's SOCKS4 and SOCKS5 branches to the differential runner's proxy responder

## Goal

The differential runner's proxy port (18300) answers SOCKS4, SOCKS4a and SOCKS5 CONNECT as `proxy_server.py` does, beside the HTTP proxy that BL-3288 ported, so BL-3234's eleven `px-socks*` and `px-preproxy` cases have a server.

## Context

Reference (never run or commit Python): `Z:\repos\Curl.difftest\2026-10-22\proxy_server.py`, class `P.handle`, lines 16-46. It peeks the first byte: `0x05` is SOCKS5 (log `SOCKS5 methods=<hex>`, reply `05 00`, read the CONNECT with atyp 1/3/4, log `SOCKS5 connect atyp={at} host={host} port={port}`, reply `05 00 00 01 00*6`, tunnel); `0x04` is SOCKS4 or SOCKS4a (read 8 bytes and a NUL-ended user id, then a NUL-ended host name when the address is `0.0.0.x`; log `SOCKS4a host={host} port={port} uid={uid!r}` or `SOCKS4 ip={ip} port={port} uid={uid!r}`, reply `00 5a` plus the request's port and address, tunnel); anything else goes to the existing `HttpProxyResponder`. Both map host `localhost.test` to `127.0.0.1` before connecting through the injected `IProxyTunnelConnector`. BL-3288 ported only the HTTP half (see its Notes). Byte reprs in the log use `ProtocolBytesRepr`.

## Acceptance criteria

- [x] A responder in `Curl.Conformance.Differential.UnitLibrary/Protocols/Proxy` serves SOCKS4, SOCKS4a and SOCKS5 CONNECT and hands every other first byte to `HttpProxyResponder`, logging the lines `proxy_server.py` logs, with unit tests in `Curl.Conformance.Differential.UnitTests/Protocols/Proxy`.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3234's lane-1 run on 2026-10-23: its sizing pass found the proxy cases' SOCKS server missing; BL-3234 depends on this.

- Plan (lane 1, 2026-10-23): `ProxyServerResponder` reads the first byte through a `DifferentialHttpLineReader`; `0x05` and `0x04` are served here, anything else (including an empty stream) goes to a new `HttpProxyResponder.RespondAsync(connection, reader, consumed, ...)` overload that puts the consumed byte back in front of the request line (a lone `\n` is the whole line, as Python's `readline` after `peek` gives). The tunnel relay and the `localhost.test` mapping moved to an internal `ProxyTunnel` both responders share.
- Decision (sensible default): a SOCKS read cut short where the script's `struct.unpack`, `inet_ntoa` or `[0]` would raise throws `EndOfStreamException`, which ends the connection without an answer, as the crashed handler did; the reads Python leaves short (methods, SOCKS5 name, IPv6 address, NUL-ended user id and host) stay short. Names are decoded as UTF-8 with replacement rather than raising on invalid bytes, an edge no case exercises.
- Verified: 608 fast tests in Curl.Conformance.Differential.UnitTests pass with -warnaserror; Measure-CodeQuality.ps1 for Curl.Conformance.Differential.UnitLibrary: 100% line and branch, 0 failing members (first run flagged `ReadRequestAsync` at complexity 14; the request-line read was extracted). Tests use in-memory streams and byte strings only, so they are platform-neutral for Linux and macOS CI.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 8c1939d6 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner's proxy responder serves SOCKS4, SOCKS4a and SOCKS5 CONNECT beside HTTP, as proxy_server.py does
