---
id: BL-5903
title: Exit 55 or 56 with curl's words when a dns:// query over --dns-transport https loses its connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Tcp/DnsTcpTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5903 — Exit 55 or 56 with curl's words when a dns:// query over --dns-transport https loses its connection

## Goal

A `dns://` query over `--dns-transport https` whose connection fails while the request is sent or the response is read exits 55 or 56 with curl's `Send failure:` / `Recv failure:` message, never .NET's exception text.

## Context

Found by BL-5874, which fixed the same gap in `Transport/Tcp/DnsTcpTransport.cs` (shared by TLS). `Transport/Https` lets a plain `IOException` from `IConnection` escape: the write in `DnsHttpsTransport`, the reads in `DohResponseReader`, and the HTTP/2 path through `DohConnectionStream` (`DohHttp2Exchange` catches HTTP/2 exceptions and `EndOfStreamException` only). Word failures with `CurlSocketErrorText` and keep a `MissingCloseNotifyException`'s message, as `DnsTcpTransport` does.

## Acceptance criteria

- [x] A test-double connection that resets while the DoH response is read, over HTTP/1.1 and over HTTP/2, exits 56 with curl's recv-failure message, pinned by unit tests.
- [x] A test-double connection that resets while the request is sent exits 55 with curl's send-failure message, pinned by a unit test.
- [x] No .NET exception text reaches stderr for these cases.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `DnsHttpsTransport` wraps the TLS connection in `DohFailureWordingConnection`, so the HTTP/1.1 and HTTP/2 paths share one
  translation. It throws `DohConnectionException`, an `IOException`, which `DnsHttpsTransport` turns into its
  `DnsQueryException`. It is an `IOException` on purpose: `Http2Connection` ignores one from the GOAWAY it sends after a
  protocol error, so a protocol error stays exit 8 when the GOAWAY cannot be sent (pinned by a test).
- The words come from `DnsTcpTransport.SendFailureOf` and `ReceiveFailureOf` (BL-5874's behaviour, moved into two methods),
  so TCP, TLS and DoH cannot drift apart. That added `DnsTcpTransport.cs` to `touches`, with the library's `CLAUDE.md`;
  no other task in Doing named either. No new ADR: the behaviour is BL-5874's, applied to DoH.
- A reset is worded per platform (`CurlSocketErrorText`), so the reset tests are pinned by `OSCondition`.
- Verified: `dotnet build` clean; fast tests green (DnsQuery 278 passed, 6 skipped); `Measure-CodeQuality.ps1` for the
  library reports 0 failing members.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: fa936731 8150d799 36a6d25e 3d836d5e.
- 2026-11-24: Doing -> Done. A DoH query that loses its connection exits 55 or 56 with curl's words, over HTTP/1.1 and HTTP/2
