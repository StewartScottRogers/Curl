---
id: BL-4729
title: Fix gRPC -H handling: a removing -H followed by another -H throws, and HTTP/2 connection-specific headers are forwarded
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Wire/GrpcRequestHeaders.cs, Curl.Extended.Protocol.Grpc.UnitTests/Wire/GrpcRequestHeadersTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4729 — Fix gRPC -H handling: a removing -H followed by another -H throws, and HTTP/2 connection-specific headers are forwarded

## Goal

Every combination of gRPC `-H` values builds valid HTTP/2 request headers: a removing `-H` followed by another `-H` no longer throws, and HTTP/2 connection-specific fields never reach the wire.

## Context

Found by the shift-end review of 2026-11-03 (code-reviewer and type-design-analyzer; diff 58b155fc2...04010e380, ADR-1227).

- `Curl.Extended.Protocol.Grpc.UnitLibrary/Wire/GrpcRequestHeaders.cs:57` takes `callDefinitionCount = headers.Count` once, before the `-H` loop. `AddCustomHeader` (line 73) calls `headers.FindIndex(0, callDefinitionCount, ...)`. When an earlier `-H "name:"` has called `RemoveAt`, the list is shorter than that count, so `FindIndex` throws `ArgumentOutOfRangeException`. `GrpcProtocolHandler` does not catch it, so it ends as `curl: (2) ...`.
  - Reproduce: `grpc://h/S/M -H "te:" -H "x-trace: abc"`.
  - Second symptom: after a removal, the search window covers the first custom header, so `-H "user-agent:" -H "x-a: 1" -H "x-a: 2"` replaces `x-a: 1` in place instead of adding a second `x-a`.
- `ParseCustomHeader` passes through every non-pseudo name. RFC 9113 section 8.2.2 forbids `connection`, `keep-alive`, `proxy-connection`, `transfer-encoding` and `upgrade`, and allows `te` only with the value `trailers`. A request that carries one is malformed, and a conforming server resets the stream. curl's own HTTP/2 layer drops these headers.

Simplest fix: apply the `-H` overrides to a separate call-definition list, then append the custom metadata. Skip the forbidden names.

## Acceptance criteria

- [ ] `GrpcRequestHeaders` builds headers for `-H "te:" -H "x-trace: abc"` without throwing; the result has no `te` and has `x-trace: abc` (unit test).
- [ ] `-H "user-agent:" -H "x-a: 1" -H "x-a: 2"` produces two `x-a` entries, in order (unit test).
- [ ] `-H "Connection: keep-alive"`, `Keep-Alive`, `Proxy-Connection`, `Transfer-Encoding` and `Upgrade` are not sent, and a `te` other than `trailers` is not sent (one unit test per name).
- [ ] `dotnet build` is clean, the fast tests pass, and the library keeps 100% line and branch coverage.
## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4ed83a1b 8cd5f741 bc5004fd a963b8b4.
