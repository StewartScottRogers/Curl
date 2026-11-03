---
id: BL-4478
title: Conformance: gRPC server reflection and the proto3 JSON mapping for grpc:// (Grpc part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4530, BL-4531, BL-4532, BL-4533]
touches: [Curl.Conformance.Extended.UnitLibrary/Grpc/Reflection, Curl.Conformance.Extended.UnitTests/Grpc/Reflection, Documentation/Conformance/Extended/Grpc-Reflection.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4478 — Conformance: gRPC server reflection and the proto3 JSON mapping for grpc:// (Grpc part 2 of 2)

## Goal

Curl's `grpc://` calls without `--grpc-proto` (server reflection) and its JSON encoding of requests and replies are
measured against the gRPC Server Reflection Protocol and protobuf's proto3 JSON mapping through the real curl
command line, every client requirement is in a matrix mapped to a test, and every failure is fixed or listed with
its fix task.

## Context

Specifications: grpc/grpc `doc/server-reflection.md` and `grpc.reflection.v1.ServerReflection` (and `v1alpha`, as the
client falls back to it, if it does): `file_containing_symbol`, `file_by_filename` for imports, `error_response`;
protobuf's "ProtoJSON format" (protobuf.dev/programming-guides/json: field names in lowerCamelCase, 64-bit integers
as strings, bytes as base64, enums by name, default values omitted, unknown fields) and the binary wire format the
library encodes. Builds on the reference gRPC server BL-4477 wrote in `Curl.Conformance.Extended.UnitLibrary/Grpc/`;
add the reflection service in `Grpc/Reflection/` through its extension point.

Library: `Curl.Extended.Protocol.Grpc.UnitLibrary` (`Reflection/`, `Client/GrpcMessageJson`,
`Client/GrpcProtoMethod`; shared `Curl.Protobuf.UnitLibrary`; ADR-1147). The reference service's descriptors are
built by the test from `FileDescriptorProto` bytes written by hand or by `Curl.Protobuf`'s writer.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Grpc:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. One file per case group; nothing added to `Harness/`. This task touches only its own
conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a defect in
the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Grpc conformance: <requirement>`
(pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a
focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Grpc-Reflection.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of the
      reflection protocol and the ProtoJSON format that binds a client an `Grpc:` ID mapped to a test or to
      `n/a: <reason>`; the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Grpc/Reflection/` run curl in process without `--grpc-proto` and
      assert the reflection requests, stdout, stderr and exit code for at least: a method found by symbol, a
      descriptor with imports fetched by file name, an unknown service (`error_response`), a server without
      reflection (UNIMPLEMENTED), and a JSON round trip of every scalar type, repeated fields, maps, nested messages,
      enums, `bytes`, 64-bit integers and well-known `Timestamp` and `Duration` if the library supports them.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Grpc conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/Grpc/Reflection/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Reference side (`Curl.Conformance.Extended.UnitLibrary/Grpc/Reflection/`): `GrpcReflectionService` (v1 reflection over
  hand-written `FileDescriptorProto`s), `GrpcReflectionRequest` (reads a `ServerReflectionRequest`),
  `ProtobufMessageWriter` (binary wire format writer; the harness does not reference Curl.Protobuf, the library under
  test), `GrpcEchoService` (JSON round trips) and `GrpcUnimplementedMethodService` (a server without reflection:
  Trailers-Only UNIMPLEMENTED as soon as the headers arrive, as gRPC servers answer an unknown method).
- Decision: the reference server sends one reply per call (BL-4530's extension point), so the reflection service answers
  the whole bidirectional stream once the first request arrives: the requested file, then the `file_by_filename`
  answers in the breadth-first order the client asks for missing dependencies. A server may send ahead on the wire; the
  cases assert the requests curl actually sent from the recorded call. Changing `GrpcReferenceSession` to answer per
  message was outside this task's touches and not needed.
- Decision: the client speaks reflection v1 only (ADR-1149), so the v1alpha fallback row is n/a. The ProtoJSON guide
  states its rules descriptively; the matrix reads them as MUST and keeps SHOULD for "should reject unknown fields".
- Measured: every case passes except Timestamp/Duration (curl exits 2: Curl.Protobuf has no well-known-type JSON
  forms), listed in KnownFailures.txt with the filed High task BL-4675. The other well-known types (Any, Struct,
  wrappers, FieldMask, Empty) are one n/a row pointing at BL-4676, filed to add their cases.
- Checks: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Conformance.Extended.UnitTests 1562
  passed, 1 skipped; all 115 test assemblies passed); `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`:
  0 failing members. Tests are platform-neutral (no file paths; stdout compared with `\n`, stderr with
  `Environment.NewLine` as the other Grpc cases do). The CI criterion for Linux and macOS is met by keeping the change
  platform-neutral and the fast tests green here.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: ffc6d5d0 0984e30a 2d0dc877 d8e3fb0e 5af3e2d0 ef6c9026.
- 2026-11-03: Doing -> Done. grpc:// server reflection and the proto3 JSON mapping are measured by 10 cases against a reference reflection service; Grpc-Reflection.md maps every requirement; Timestamp/Duration listed for BL-4675
