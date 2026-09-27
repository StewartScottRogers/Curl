---
id: BL-345
title: Send tftp:// through an HTTP proxy as the reference build does and fail with exit 7
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-345 — Send tftp:// through an HTTP proxy as the reference build does and fail with exit 7

## Goal

`curl -x http://<proxy> tftp://…` sends the reference build's MASQUE request to the proxy and ends with exit 7 `bind() failed; Invalid arguments`, never sending TFTP datagrams around the proxy.

## Context

- ADR-0056, rule 4.
- Measured by BL-330: `curl -sS -x http://127.0.0.1:18331 tftp://example.com/f` sent `GET http://127.0.0.1:18331/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\nHost: 127.0.0.1:18331\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n`, then wrote `curl: (7) bind() failed; Invalid arguments` to stderr and exited 7.
- `TftpProtocolHandler` takes only an `IDatagramConnector`; sending the request needs a TCP `IConnector` too. If that widens the task, split it rather than grow it.
- Where a criterion says *measured*, the bytes were measured by BL-330 with curl 8.21.0 (`/mingw64/bin/curl`, ADR-0009) against a loopback listener and are recorded under BL-330's `Notes`. Pin only those bytes; re-measure with `Record-CurlExchange.ps1` for anything else.

## Acceptance criteria

- [ ] A test shows a context with an HTTP `Proxy` sends no datagram and returns exit 7 with `bind() failed; Invalid arguments`.
- [ ] A test pins the measured MASQUE request bytes sent to the proxy.
- [ ] A context without a proxy behaves as before; 100% line and branch coverage of the changed code.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
