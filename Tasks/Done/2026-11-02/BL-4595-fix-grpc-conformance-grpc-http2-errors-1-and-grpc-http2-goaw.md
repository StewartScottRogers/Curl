---
id: BL-4595
title: Fix Grpc conformance: Grpc:Http2-Errors-1 and Grpc:Http2-GoAway-1 - report an RST_STREAM or GOAWAY as its mapped gRPC status
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---

## Goal

A `grpc://` call the server ends with RST_STREAM, or with a GOAWAY whose last stream ID is below the call's stream,
fails with exit 22 and `curl: (22) grpc-status <code> (<name>): ...`, the code PROTOCOL-HTTP2.md maps it to.

## Context

Found by BL-4585. `GrpcProtocolHandler.ExecuteAsync` catches `Http2StreamResetException` and `Http2GoAwayException`
with the other HTTP/2 failures and returns exit 16 (CURLE_HTTP2) with the exception text. ADR-1202 decides they are
reported as gRPC statuses instead: RST_STREAM by the Errors table (NO_ERROR, PROTOCOL_ERROR, INTERNAL_ERROR,
FLOW_CONTROL_ERROR, SETTINGS_TIMEOUT, FRAME_SIZE_ERROR, COMPRESSION_ERROR, CONNECT_ERROR to INTERNAL 13; REFUSED_STREAM
to UNAVAILABLE 14; CANCEL to CANCELLED 1; ENHANCE_YOUR_CALM to RESOURCE_EXHAUSTED 8; INADEQUATE_SECURITY to
PERMISSION_DENIED 7), a GOAWAY that did not accept the call as UNAVAILABLE 14. The cases are
`Curl.Conformance.Extended.UnitTests/Grpc/GrpcTransportCases.cs`
(`RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo`, `GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable`).

## Acceptance criteria

- [x] Both cases pass and the lines `Grpc:Http2-Errors-1 BL-4591` and `Grpc:Http2-GoAway-1 BL-4591` are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.Grpc.UnitTests` pins the mapping of each RST_STREAM error code and of a GOAWAY (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Grpc.UnitLibrary` stays inside every quality gate.

## Notes

- Delivered directly (one handler, one table): `GrpcProtocolHandler.ExecuteAsync` now catches `Http2StreamResetException` as the status `StatusOfReset` maps its error code to (a dictionary of the four non-INTERNAL rows, so complexity stays low; every other code, STREAM_CLOSED and HTTP_1_1_REQUIRED included, is INTERNAL) and `Http2GoAwayException` as UNAVAILABLE 14, both exit 22 with `grpc-status <code> (<name>): <the exception text>`.
- Choice: every `Http2GoAwayException` is UNAVAILABLE. Http2Connection throws it only for a GOAWAY with an error code or one that leaves the call's stream unprocessed; the handler never sees the stream id, and a GOAWAY that ends the one call on a fresh connection is the transport going away, which gRPC reports as UNAVAILABLE.
- Checks: Grpc.UnitTests 183 passed; Conformance.Extended.UnitTests 667 passed with both KnownFailures lines removed; `dotnet build Curl.slnx -warnaserror` clean; solution fast tests green; Measure-CodeQuality for Curl.Extended.Protocol.Grpc.UnitLibrary: 0 failing members.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: bcc36540 fef5bce7 a1a024a4.
- 2026-11-02: Doing -> Done. A grpc:// call ended by RST_STREAM or GOAWAY fails with exit 22 and its mapped grpc-status
