---
id: BL-4477
title: Conformance: gRPC over HTTP/2 (PROTOCOL-HTTP2.md) for grpc:// and grpcs:// (Grpc part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4501, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Grpc, Curl.Conformance.Extended.UnitTests/Grpc, Documentation/Conformance/Extended/Grpc.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4477 — Conformance: gRPC over HTTP/2 (PROTOCOL-HTTP2.md) for grpc:// and grpcs:// (Grpc part 1 of 2)

## Goal

Curl's `grpc://` and `grpcs://` client is measured against grpc.io's "gRPC over HTTP2" protocol document through
the real curl command line with `--grpc-proto`, every client requirement is in a matrix mapped to a test, and every
failure is fixed or listed with its fix task.

## Context

Specification: grpc/grpc `doc/PROTOCOL-HTTP2.md` ("gRPC over HTTP2": Request-Headers with `:method POST`,
`:scheme`, `:path /Service/Method`, `te: trailers`, `content-type application/grpc`, `grpc-timeout` units and
digit limit, `grpc-encoding`, custom metadata and `-bin` base64; Length-Prefixed-Message; Response-Headers, Trailers
and Trailers-Only; `grpc-status`, `grpc-message` percent-encoding; HTTP status to gRPC code mapping) and
`doc/statuscodes.md`. Part 2 (BL-4478) covers server reflection and the JSON mapping.

Library: `Curl.Extended.Protocol.Grpc.UnitLibrary` (`CLAUDE.md`: `Wire/`, `Client/`; ADR-1133, ADR-1147). The
reference server sits on `Curl.Conformance.Http2Server.UnitLibrary`; `grpcs://` adds the harness's TLS route with
ALPN `h2`. Example: `curl --grpc-proto svc.proto -d '{"name":"x"}' grpc://host/pkg.Service/Method` (the `.proto` file
in a temporary directory the test creates and deletes).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Grpc:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference server implements the gRPC framing from the specification itself and
references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Grpc conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Grpc.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      PROTOCOL-HTTP2.md that binds a client a `Grpc:` ID mapped to a test or to `n/a: <reason>`, links to
      `Grpc-Reflection.md` (BL-4478), and the harness's matrix check passes.
- [ ] A reference gRPC server in `Curl.Conformance.Extended.UnitLibrary/Grpc/` lets BL-4478 add the reflection service
      in `Grpc/Reflection/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Grpc/` run curl in process and assert HTTP/2 frames and header
      lists, stdout, stderr and exit code for at least: a unary call, a server-streaming reply, Trailers-Only, every
      status code's name, a `grpc-message` needing percent-decoding, a non-200 HTTP status mapped to its code, a
      message split across DATA frames, a truncated message, a compressed-flag message the client cannot decode,
      `grpc-timeout` from `-m`, and `grpcs://` with ALPN `h2`.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Grpc conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Grpc/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
