---
id: BL-4730
title: Fix DoH over HTTP/2: map HTTP/2 and HPACK failures to DnsQueryException and cap the answer at 65535 octets
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https/DohHttp2Exchange.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Https/DohHttp2ExchangeTests.cs, Documentation/Planning/Decisions/ADR-1231-dns-over-https-prefers-http2-through-alpn.md]
requirement: none
created: 2026-11-03
completed:
---
# BL-4730 — Fix DoH over HTTP/2: map HTTP/2 and HPACK failures to DnsQueryException and cap the answer at 65535 octets

## Goal

A DoH query over HTTP/2 that the server resets, refuses with GOAWAY, breaks mid-frame, answers with bad HPACK or answers with more than 65535 octets ends with a `DnsQueryException` and its curl exit code, never `curl: (2)` and never unbounded memory.

## Context

Found by the shift-end review of 2026-11-03 (silent-failure-hunter, code-reviewer and type-design-analyzer; diff 58b155fc2...04010e380, ADR-1231).

- `Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https/DohHttp2Exchange.cs:31-74` catches nothing from the HTTP/2 layer:
  - `Http2Connection.ReadStreamFrameAsync` throws `Http2StreamResetException`, `Http2GoAwayException`, `Http2ProtocolException` and `EndOfStreamException`.
  - `HpackDecoder.Decode` throws `HpackDecodingException`.
  - `DnsQueryProtocolHandler.ExecuteAsync` (line 33) catches only `DnsQueryException`, so each of these reaches `UnhandledExceptionGuard` as exit 2.
  - This contradicts the method's own `<exception>` doc and ADR-1231, which promise exit 56 when the connection closes early.
  - `GrpcProtocolHandler.cs:48-59` already maps the same exceptions and can serve as the model.
- Line 64 appends every DATA frame (`body.AddRange`) with no limit. The HTTP/1.1 path rejects an answer over `DohResponseHead.MaxMessageLength` with exit 8 (`DohResponseReader.cs:147`); the HTTP/2 path does not.

## Acceptance criteria

- [x] Each of these, sent by a scripted HTTP/2 peer, ends in a `DnsQueryException` with the stated exit code (one unit test each in `DohHttp2ExchangeTests`):
  - RST_STREAM: 56
  - GOAWAY: 56
  - the connection closed mid-frame: 56
  - a malformed HPACK block: 8, or 16 if the ADR records it
  - an HTTP/2 protocol error: 8, or 16 if the ADR records it
- [x] An answer whose DATA frames total more than 65535 octets fails with exit 8 and the same message `DohResponseReader` uses, before more than 65535 octets are buffered (unit test).
- [x] ADR-1231 states the error mapping the code applies.
- [x] `dotnet build` is clean, the fast tests pass, and the library keeps 100% line and branch coverage.
## Notes

- Decided: HPACK and HTTP/2 protocol errors are exit 8 (not 16), recorded in ADR-1231. Reset, GOAWAY and mid-frame close are exit 56. The 65535 check sits in `AppendWithinLimit` to stay under the complexity gate. Quality run for the library: 0 failing members, 100% line and branch.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 51df3932 ad459068 c42bcdef a963b8b4.
