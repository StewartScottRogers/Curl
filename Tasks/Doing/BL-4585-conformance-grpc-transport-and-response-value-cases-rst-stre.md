---
id: BL-4585
title: Conformance: gRPC transport and response-value cases - RST_STREAM, GOAWAY, PING, invalid grpc-message and metadata values (Grpc part 1e)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4532]
touches: [Curl.Conformance.Extended.UnitLibrary/Grpc/GrpcReferenceReply.cs, Curl.Conformance.Extended.UnitLibrary/Grpc/GrpcReferenceSession.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcReferenceServerTests.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcTransportCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcResponseValueCases.cs, Documentation/Conformance/Extended/Grpc-Transport.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
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

- [ ] `GrpcReferenceReply` can script an RST_STREAM with an error code, a GOAWAY with a last stream ID, and a PING sent
      before the reply that records whether curl echoed its payload; `GrpcReferenceServerTests` covers each.
- [ ] `Curl.Conformance.Extended.UnitTests/Grpc/GrpcTransportCases.cs` covers the RST_STREAM, GOAWAY and PING
      requirements above, and `GrpcResponseValueCases.cs` the invalid `grpc-message` and invalid ASCII-Value ones.
- [ ] `Documentation/Conformance/Extended/Grpc-Transport.md` maps each of those requirements to its case, links to
      `Grpc.md`, and the harness's matrix check passes.
- [ ] Every failing case is fixed here or listed in `KnownFailures.txt` with the ID of its filed High
      `Fix Grpc conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 1751e4d7 5a446c73 cf7d1a20 36ff140a 3af8a194.
