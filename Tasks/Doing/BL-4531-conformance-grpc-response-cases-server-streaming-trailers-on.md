---
id: BL-4531
title: Conformance: gRPC response cases - server streaming, Trailers-Only, status names, grpc-message, HTTP status mapping (Grpc part 1b)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4530]
touches: [Curl.Conformance.Extended.UnitTests/Grpc/GrpcResponseCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcStatusCases.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4531 — Conformance: gRPC response cases - server streaming, Trailers-Only, status names, grpc-message, HTTP status mapping (Grpc part 1b)

## Goal

Conformance cases pin curl's handling of gRPC responses: server streaming, Trailers-Only, status codes, grpc-message
and HTTP status mapping.
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
Uses the reference server BL-4530 adds; adds no file under `Curl.Conformance.Extended.UnitLibrary/Grpc/`.

## Acceptance criteria

- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Grpc/` assert frames, stdout, stderr and exit code for a
      server-streaming reply, a Trailers-Only reply, every status code's name (statuscodes.md), a `grpc-message`
      needing percent-decoding and a non-200 HTTP status mapped to its gRPC code, each carrying its `Grpc:` ID.
- [ ] Every failing case is fixed here (if the fault is in the cases) or listed in `KnownFailures.txt` with the ID of
      its filed High `Fix Grpc conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.
## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8c75d14b d3c3b130 3af8a194.
