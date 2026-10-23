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
completed:
---
# BL-3334 — Port proxy_server.py's SOCKS4 and SOCKS5 branches to the differential runner's proxy responder

## Goal

The differential runner's proxy port (18300) answers SOCKS4, SOCKS4a and SOCKS5 CONNECT as `proxy_server.py` does, beside the HTTP proxy that BL-3288 ported, so BL-3234's eleven `px-socks*` and `px-preproxy` cases have a server.

## Context

Reference (never run or commit Python): `Z:\repos\Curl.difftest\2026-10-22\proxy_server.py`, class `P.handle`, lines 16-46. It peeks the first byte: `0x05` is SOCKS5 (log `SOCKS5 methods=<hex>`, reply `05 00`, read the CONNECT with atyp 1/3/4, log `SOCKS5 connect atyp={at} host={host} port={port}`, reply `05 00 00 01 00*6`, tunnel); `0x04` is SOCKS4 or SOCKS4a (read 8 bytes and a NUL-ended user id, then a NUL-ended host name when the address is `0.0.0.x`; log `SOCKS4a host={host} port={port} uid={uid!r}` or `SOCKS4 ip={ip} port={port} uid={uid!r}`, reply `00 5a` plus the request's port and address, tunnel); anything else goes to the existing `HttpProxyResponder`. Both map host `localhost.test` to `127.0.0.1` before connecting through the injected `IProxyTunnelConnector`. BL-3288 ported only the HTTP half (see its Notes). Byte reprs in the log use `ProtocolBytesRepr`.

## Acceptance criteria

- [ ] A responder in `Curl.Conformance.Differential.UnitLibrary/Protocols/Proxy` serves SOCKS4, SOCKS4a and SOCKS5 CONNECT and hands every other first byte to `HttpProxyResponder`, logging the lines `proxy_server.py` logs, with unit tests in `Curl.Conformance.Differential.UnitTests/Protocols/Proxy`.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3234's lane-1 run on 2026-10-23: its sizing pass found the proxy cases' SOCKS server missing; BL-3234 depends on this.

## Log

- 2026-10-23: Created.
