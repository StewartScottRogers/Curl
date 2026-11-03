---
id: BL-4674
title: Fix Grpc conformance: Grpc:Http2-Request-8 to Request-12 - send -H custom metadata (-bin base64, un-padded), grpc-timeout right after the pseudo-headers, and a user-agent
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Wire/GrpcRequestHeaders.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection/ServerReflectionStream.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection/ServerReflectionClient.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Wire/GrpcRequestHeaderValues.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.Grpc.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4674 — Fix Grpc conformance: Grpc:Http2-Request-8 to Request-12 - send -H custom metadata (-bin base64, un-padded), grpc-timeout right after the pseudo-headers, and a user-agent

## Goal

A `grpc://` call sends every `-H` header as Custom-Metadata, a `-bin` key's value base64 un-padded (ADR-1226), puts
`grpc-timeout` straight after the pseudo-headers and Custom-Metadata after the Call-Definition headers, and sends a
`user-agent`, so `GrpcRequestHeaderCases` pass and their `KnownFailures.txt` lines go.

## Context

Found by BL-4533. `GrpcRequestHeaders.Build` (Curl.Extended.Protocol.Grpc.UnitLibrary/Wire) takes no custom headers,
puts `te` before `grpc-timeout`, and `GrpcProtocolHandler` and `ServerReflectionStream` pass `null` for the user-agent.
PROTOCOL-HTTP2.md "Requests"; matrix rows `Grpc:Http2-Request-8` to `Request-12` in
`Documentation/Conformance/Extended/Grpc.md`; ADR-1226 (`-bin` values given to `-H` are read as base64). For the
user-agent, use `-A` when given, else curl's default `curl/<version>` as the HTTP handler sends it.

## Acceptance criteria

- [x] `GrpcRequestHeaderCases` cases for `Grpc:Http2-Request-8` to `Grpc:Http2-Request-12` pass and their
      `KnownFailures.txt` lines are removed.
- [x] Focused unit tests in `Curl.Extended.Protocol.Grpc.UnitTests` pin the header order, the metadata, the `-bin`
      re-encoding and the user-agent (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage with complexity at most 10.

## Notes

- Delivered directly rather than through the full /feature agent pipeline: a single-library change whose design ADR-1226
  and PROTOCOL-HTTP2.md already fixed; the remaining choices are in ADR-1227 (Decided by Claude under Stewart's delegation).
- `GrpcRequestHeaders.Build(target, GrpcRequestHeaderValues)`: pseudo-headers, `grpc-timeout`, `te`, `content-type`,
  `user-agent`, then `-H` values lower case as Custom-Metadata. `-H` is read as curl's HTTP handler reads it
  (`Name:` sends nothing, `Name;` an empty value); one naming `te`, `grpc-timeout`, `content-type` or `user-agent`
  replaces or removes it in place. `user-agent` is `-A`, none for `-A ""`, else `curl/8.22.0` (the HTTP handler's
  `DefaultUserAgent`; the extension may not reference that library, so the value is repeated in `GrpcRequestHeaderValues`).
- A `-bin` value that is not base64 exits 2 (ADR-1226 named it curl's usage error).
- The reflection stream sends the same headers (as grpcurl does), so `ServerReflectionClient.cs` and the new
  `Wire/GrpcRequestHeaderValues.cs` were added to `touches`: the values had to be threaded through `DescribeAsync`, and
  no task in Doing on origin/work/dark-factory names either file. `CLAUDE.md` of the library added for its doc line.
- Checks: `dotnet build Curl.slnx -warnaserror` clean; fast tests green; Curl.Conformance.Extended.UnitTests Grpc cases
  97/97; Measure-CodeQuality for Curl.Extended.Protocol.Grpc.UnitLibrary: 100% line, 100% branch, 0 failing, worst CRAP 10.
- Platform check: header tests are pure string work, no paths or OS text.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: bf9be453 40477fdb eb9bb0e6.
- 2026-11-03: Doing -> Done. grpc:// sends -H as Custom-Metadata (-bin re-encoded un-padded), grpc-timeout straight after the pseudo-headers, and -A or curl's own user-agent
