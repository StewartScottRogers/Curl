---
id: BL-4674
title: Fix Grpc conformance: Grpc:Http2-Request-8 to Request-12 - send -H custom metadata (-bin base64, un-padded), grpc-timeout right after the pseudo-headers, and a user-agent
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Wire/GrpcRequestHeaders.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection/ServerReflectionStream.cs, Curl.Extended.Protocol.Grpc.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
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

- [ ] `GrpcRequestHeaderCases` cases for `Grpc:Http2-Request-8` to `Grpc:Http2-Request-12` pass and their
      `KnownFailures.txt` lines are removed.
- [ ] Focused unit tests in `Curl.Extended.Protocol.Grpc.UnitTests` pin the header order, the metadata, the `-bin`
      re-encoding and the user-agent (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage with complexity at most 10.

## Notes

## Log

- 2026-11-03: Created.
