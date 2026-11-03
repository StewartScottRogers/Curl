---
id: BL-4533
title: Conformance: gRPC requirement matrix Grpc.md for PROTOCOL-HTTP2.md, remaining request-header cases and quality gate (Grpc part 1d)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4530, BL-4531, BL-4532, BL-4585]
touches: [Documentation/Conformance/Extended/Grpc.md, Curl.Conformance.Extended.UnitTests/Grpc/GrpcRequestHeaderCases.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4533 — Conformance: gRPC requirement matrix Grpc.md for PROTOCOL-HTTP2.md, remaining request-header cases and quality gate (Grpc part 1d)

## Goal

`Documentation/Conformance/Extended/Grpc.md` maps every client requirement of PROTOCOL-HTTP2.md to a test, and the
requirements no earlier part covered (request headers such as `grpc-encoding`, custom metadata and `-bin` base64)
get their cases. This is what was left of BL-4477 after its split.
## Context

Split from BL-4477 for size (one run could not build the server, the cases and the matrix). Read BL-4477's
Context (in `Tasks/Deferred/`): specification grpc/grpc `doc/PROTOCOL-HTTP2.md` and `doc/statuscodes.md`; library
under test `Curl.Extended.Protocol.Grpc.UnitLibrary` (ADR-1133, ADR-1147); follow
`Documentation/Conformance/Extended/README.md` and ADR-1186 (`[SpecRequirement("Grpc:<source>-<locator>")]`, matrix
format, `KnownFailures.txt` rule). Measure against the specification, never against upstream curl. A defect in the
extension library is listed in `KnownFailures.txt` with a filed High task `Fix Grpc conformance: <requirement>`
(pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a
focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).
Cases run curl in process through the harness, e.g. `curl --grpc-proto svc.proto -d '{"name":"x"}'
grpc://host/pkg.Service/Method`, the `.proto` file in a temporary directory the test creates and deletes.
Builds on BL-4530 (server, unary case), BL-4531 (response cases) and BL-4532 (framing, timeout, TLS cases).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Grpc.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      PROTOCOL-HTTP2.md that binds a client a `Grpc:` ID mapped to a test or to `n/a: <reason>`, links to
      `Grpc-Reflection.md` (BL-4478), and the harness's matrix check passes.
- [x] `Curl.Conformance.Extended.UnitTests/Grpc/GrpcRequestHeaderCases.cs` covers the request-header requirements no
      other Grpc case carries (`grpc-encoding`, custom metadata, `-bin` base64 metadata).
- [x] Every failing case is fixed here or listed in `KnownFailures.txt` with the ID of its filed High
      `Fix Grpc conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.
## Notes

- 2026-11-02 (lane 15, first pass, no code edited): PROTOCOL-HTTP2.md's client requirements include RST_STREAM handling
  and its error-code table, GOAWAY ("streams after the last accepted are UNAVAILABLE"), PING echo, and two response
  MUST NOTs (invalid `grpc-message` percent-encoding, invalid ASCII-Value metadata). `GrpcReferenceServer` cannot send
  RST_STREAM, GOAWAY or its own PING, so those need reference-server work no task covered: filed BL-4585 (it adds
  `Grpc-Transport.md`), added to `depends-on`, requeued. Once it is Done, this task's `Grpc.md` links to
  `Grpc-Transport.md` and `Grpc-Responses.md` for those rows.
- Measured for the next claim: `GrpcRequestHeaders.Build` (Curl.Extended.Protocol.Grpc.UnitLibrary/Wire) sends no
  `-H` custom metadata, no `user-agent` (`GrpcProtocolHandler` passes `null`), no `grpc-encoding`, and puts `te` before
  `grpc-timeout` (spec: "send Timeout immediately after the reserved headers ... Call-Definition headers before
  Custom-Metadata"). So the metadata, `-bin` (MUST base64, SHOULD un-padded), header-order and user-agent
  (recommended) cases will fail: list them in `KnownFailures.txt` under one filed High
  `Fix Grpc conformance: request headers ...` task. The `grpc-encoding` case passes: no `grpc-encoding` is sent and
  every request Compressed-Flag is 0 ("If the Message-Encoding header is omitted then the Compressed-Flag must be 0").
- Rows that are n/a for a client: SHOULD 415 for a wrong Content-Type (server), "Status must be sent in Trailers"
  (server), `grpc-` reserved names and echoing metadata (application), compression contexts (curl compresses
  nothing), receiving `-bin` padded or un-padded and splitting on "," (curl decodes no received binary metadata),
  `grpc-status-details-bin` "MUST NOT contradict / consumer MUST verify" (curl does not decode status details),
  GOAWAY before closing (server). `Grpc-Reflection.md` does not exist yet (BL-4478 in Backlog); link it anyway, as
  the criterion asks.

- 2026-11-03 (lane 3): Added `GrpcRequestHeaderCases` (Grpc:Http2-Request-7 to 12) and Grpc.md rows for them plus n/a rows (Request-13 to 15, Response-7, Response-8, Compression-1, GoAway-2); Grpc.md links Grpc-Responses.md, Grpc-Transport.md and Grpc-Reflection.md (the last not yet written, BL-4478). Request-7 (no grpc-encoding, every Compressed-Flag 0) passes; Request-8 to 12 fail as measured and are listed in KnownFailures.txt under filed High BL-4674. Decided `-H "<key>-bin: <value>"` reads the value as base64 and sends it un-padded, as grpcurl and grpc-go do: ADR-1226. The user-agent case asks only for a non-empty `user-agent` (the spec only recommends a structure). Platform-neutral: temp directory via Directory.CreateTempSubdirectory, loopback in process, no OS text. `dotnet build Curl.slnx -warnaserror` clean, fast tests green (Curl.Conformance.Extended.UnitTests 1537 passed). No production code changed, so no quality run.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 123b988a abb71175 36ff140a 3af8a194.
- 2026-11-02: Doing -> Backlog. Waits on BL-4585: the reference server cannot yet send RST_STREAM, GOAWAY or PING, which the client requirements in the matrix need
- 2026-11-03: Backlog -> Doing. Criteria at claim: 123b988a abb71175 36ff140a 3af8a194.
- 2026-11-03: Doing -> Done. Grpc.md maps every client requirement of PROTOCOL-HTTP2.md; request-header cases added, failures listed under BL-4674
