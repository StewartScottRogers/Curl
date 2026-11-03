---
id: BL-4585
title: Conformance: gRPC transport and response-value cases - RST_STREAM, GOAWAY, PING, invalid grpc-message and metadata values (Grpc part 1e)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4532]
touches: [Curl.Conformance.Extended.UnitLibrary/Grpc/GrpcReferenceReply.cs, Curl.Conformance.Extended.UnitLibrary/Grpc/GrpcReferenceSession.cs, Curl.Conformance.Extended.UnitLibrary/Grpc/GrpcReferencePing.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcReferenceServerTests.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcTransportCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcResponseValueCases.cs, Documentation/Conformance/Extended/Grpc-Transport.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4585 — Conformance: gRPC transport and response-value cases - RST_STREAM, GOAWAY, PING, invalid grpc-message and metadata values (Grpc part 1e)

## Goal

The client requirements of PROTOCOL-HTTP2.md that need the server to send RST_STREAM, GOAWAY or PING, or a malformed
`grpc-message` or metadata value, each have a case and a row in `Documentation/Conformance/Extended/Grpc-Transport.md`,
so BL-4533 can complete the gRPC matrix.

## Context

Found by BL-4533's first pass: `GrpcReferenceServer` (`Curl.Conformance.Extended.UnitLibrary/Grpc/`) answers PING but
cannot send RST_STREAM, GOAWAY or a PING of its own, and no case covers the response-value MUST NOTs. Specification
grpc/grpc `doc/PROTOCOL-HTTP2.md`; follow `Documentation/Conformance/Extended/README.md` and ADR-1186
(`[SpecRequirement("Grpc:Http2-<section>-<n>")]`, matrix format, `KnownFailures.txt` rule); measure against the
specification, never against upstream curl. A defect in `Curl.Extended.Protocol.Grpc.UnitLibrary` is listed in
`KnownFailures.txt` with a filed High task `Fix Grpc conformance: <requirement>` (pipeline feature; touches the
library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix
(ADR-0482), the line is removed). The client requirements to cover (spec wording):
- Errors: "RPC runtime implementations should interpret RST_STREAM as immediate full-closure of the stream and should
  propagate an error up to the calling application layer", with the RST_STREAM error-code to status table
  (NO_ERROR, PROTOCOL_ERROR, INTERNAL_ERROR, FLOW_CONTROL_ERROR, ... map to INTERNAL; REFUSED_STREAM to UNAVAILABLE;
  CANCEL to CANCELLED; ENHANCE_YOUR_CALM to RESOURCE_EXHAUSTED; INADEQUATE_SECURITY to PERMISSION_DENIED).
- GOAWAY: "Clients should consider any stream initiated after the last successfully accepted stream as UNAVAILABLE".
- PING: "a PING frame that the peer must respond to by precisely echoing what they received".
- Responses: "When decoding invalid values [of grpc-message], implementations MUST NOT error or throw away the message"
  (an invalid percent-encoding is shown raw).
- Request-Headers: "Implementations must not error due to receiving an invalid ASCII-Value that's a valid field-value
  in HTTP" (a response metadata value outside %x20-%x7E does not fail the call).

## Acceptance criteria

- [x] `GrpcReferenceReply` can script an RST_STREAM with an error code, a GOAWAY with a last stream ID, and a PING sent
      before the reply that records whether curl echoed its payload; `GrpcReferenceServerTests` covers each.
- [x] `Curl.Conformance.Extended.UnitTests/Grpc/GrpcTransportCases.cs` covers the RST_STREAM, GOAWAY and PING
      requirements above, and `GrpcResponseValueCases.cs` the invalid `grpc-message` and invalid ASCII-Value ones.
- [x] `Documentation/Conformance/Extended/Grpc-Transport.md` maps each of those requirements to its case, links to
      `Grpc.md`, and the harness's matrix check passes.
- [x] Every failing case is fixed here or listed in `KnownFailures.txt` with the ID of its filed High
      `Fix Grpc conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.

## Notes

- Touches widened by `Curl.Conformance.Extended.UnitLibrary/Grpc/GrpcReferencePing.cs` (new file, one public type per file): the PING that records its echo. No other task in Doing names it.
- `GrpcReferenceReply` gained `ResetCode`, `GoAwayLastStreamId` and `PingBeforeReply` (init properties) and the `Reset` and `GoAway` factories; RST_STREAM and GOAWAY (NO_ERROR) are sent in place of the reply, the PING before it. STREAM_CLOSED has no row in the Errors table, so it has no case.
- Decided (ADR-1202): an RST_STREAM or GOAWAY ending a call is reported like a non-OK status, exit 22 and `curl: (22) grpc-status <code> (<name>): `; the cases pin that prefix only. Curl today exits 16, so Grpc:Http2-Errors-1 and Grpc:Http2-GoAway-1 are in KnownFailures.txt under BL-4595 (High). Grpc:Http2-Ping-1, Grpc:Http2-Response-6 and Grpc:Http2-Request-6 pass.
- The ASCII-Value case uses a tab (valid HTTP field-value, outside %x20-%x7E). Cases are in-memory and platform-neutral.
- Checked: `dotnet build Curl.slnx -warnaserror` clean, fast tests green (no failures), Measure-CodeQuality for Curl.Conformance.Extended.UnitLibrary: 0 failing members.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 1751e4d7 5a446c73 cf7d1a20 36ff140a 3af8a194.
- 2026-11-02: Doing -> Done. gRPC RST_STREAM, GOAWAY, PING, invalid grpc-message and metadata-value cases run against the reference server, mapped in Grpc-Transport.md
