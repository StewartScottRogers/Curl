---
id: BL-4516
title: Conformance: gRPC message framing, timeout and grpcs cases - split DATA, truncated, compressed flag, grpc-timeout from -m, ALPN h2 (Grpc part 1c)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4514]
touches: [Curl.Conformance.Extended.UnitTests/Grpc/GrpcFramingCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcTimeoutCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcTlsCases.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4516 — Conformance: gRPC message framing, timeout and grpcs cases - split DATA, truncated, compressed flag, grpc-timeout from -m, ALPN h2 (Grpc part 1c)

## Goal

Conformance cases pin curl's gRPC message framing, its `grpc-timeout` from `-m` and `grpcs://` with ALPN `h2`.
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
Uses the reference server BL-4514 adds; adds no file under `Curl.Conformance.Extended.UnitLibrary/Grpc/`. The TLS
route is the harness's (`ReferenceRoute` TLS, `ReferenceCertificate.CreateSelfSigned`, curl under `-k`); the timeout
runs on the harness's fake clock (BL-4503).

## Acceptance criteria

- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Grpc/` assert frames, stdout, stderr and exit code for a message
      split across DATA frames, a truncated message, a compressed-flag message the client cannot decode,
      `grpc-timeout` from `-m` (units and digit limit) and `grpcs://` negotiating ALPN `h2`, each carrying its
      `Grpc:` ID.
- [ ] Every failing case is fixed here (if the fault is in the cases) or listed in `KnownFailures.txt` with the ID of
      its filed High `Fix Grpc conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.
## Notes

## Log

- 2026-11-02: Created.
