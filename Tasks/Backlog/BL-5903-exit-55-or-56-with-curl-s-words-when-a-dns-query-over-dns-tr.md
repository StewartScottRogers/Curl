---
id: BL-5903
title: Exit 55 or 56 with curl's words when a dns:// query over --dns-transport https loses its connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Https]
requirement: none
created: 2026-11-24
completed:
---
# BL-5903 — Exit 55 or 56 with curl's words when a dns:// query over --dns-transport https loses its connection

## Goal

A `dns://` query over `--dns-transport https` whose connection fails while the request is sent or the response is read exits 55 or 56 with curl's `Send failure:` / `Recv failure:` message, never .NET's exception text.

## Context

Found by BL-5874, which fixed the same gap in `Transport/Tcp/DnsTcpTransport.cs` (shared by TLS). `Transport/Https` lets a plain `IOException` from `IConnection` escape: the write in `DnsHttpsTransport`, the reads in `DohResponseReader`, and the HTTP/2 path through `DohConnectionStream` (`DohHttp2Exchange` catches HTTP/2 exceptions and `EndOfStreamException` only). Word failures with `CurlSocketErrorText` and keep a `MissingCloseNotifyException`'s message, as `DnsTcpTransport` does.

## Acceptance criteria

- [ ] A test-double connection that resets while the DoH response is read, over HTTP/1.1 and over HTTP/2, exits 56 with curl's recv-failure message, pinned by unit tests.
- [ ] A test-double connection that resets while the request is sent exits 55 with curl's send-failure message, pinned by a unit test.
- [ ] No .NET exception text reaches stderr for these cases.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
