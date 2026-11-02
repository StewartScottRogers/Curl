---
id: BL-4530
title: Conformance: gRPC reference server over HTTP/2 and a unary call case (Grpc part 1a)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Grpc, Curl.Conformance.Extended.UnitLibrary/Curl.Conformance.Extended.UnitLibrary.csproj, Curl.Conformance.Extended.UnitLibrary/CLAUDE.md, Curl.Conformance.Extended.UnitTests/Grpc/GrpcReferenceServerTests.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcUnaryCases.cs, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4530 — Conformance: gRPC reference server over HTTP/2 and a unary call case (Grpc part 1a)

## Goal

A reference gRPC server in `Curl.Conformance.Extended.UnitLibrary/Grpc/` serves HTTP/2 with gRPC framing written
from the specification, and a unary call case runs curl against it.
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
The server needs HPACK and HTTP/2 framing: reference `Curl.Http2.UnitLibrary` (HpackDecoder, HpackEncoder,
Http2FrameCodec) from the library's csproj, or `Curl.Conformance.Http2Server.UnitLibrary`, and say which in
`CLAUDE.md`; never a `Curl.Extended.Protocol.*` project (VerifyExtensionIsolation). Script the reply so later cases
(BL-4531, BL-4532) choose response headers, DATA frame splits, trailers or Trailers-Only, HTTP status and
compressed-flag messages without editing the server, and BL-4478 can add the reflection service in
`Grpc/Reflection/` without editing these files. The server records the request's HEADERS (decoded header list) and
DATA frames so cases assert them.

## Acceptance criteria

- [x] A reference gRPC server in `Curl.Conformance.Extended.UnitLibrary/Grpc/` (one file per type) serves `grpc://`
      over HTTP/2 prior knowledge and records the request's decoded header list and DATA frames.
- [x] Its reply is scripted by the case (status, headers, message frames and their split, trailers, Trailers-Only),
      and BL-4478 can add a service in `Grpc/Reflection/` without editing these files.
- [x] `Curl.Conformance.Extended.UnitTests/Grpc/GrpcUnaryCases.cs` runs a unary call and asserts `:method POST`,
      `:scheme`, `:path /Service/Method`, `te: trailers`, `content-type application/grpc`, the Length-Prefixed-Message,
      stdout, stderr and exit code, each assertion carrying its `Grpc:` requirement ID.
- [x] The code added under `Grpc/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30
      (`Measure-CodeQuality.ps1`), from `GrpcReferenceServerTests.cs` and the case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.
## Notes

- Server: `GrpcReferenceServer` (one `GrpcReferenceSession` per connection) on `Curl.Http2.UnitLibrary`'s HPACK and
  frame codec, not `Curl.Conformance.Http2Server.UnitLibrary` (a relay to a backend, not a scriptable endpoint); said
  in the library's `CLAUDE.md`. It records each `GrpcReferenceCall` (decoded header list, DATA frames, `Messages`) and
  offers the call to its `IGrpcReferenceService`s after the headers and after each DATA frame, so BL-4478's reflection
  service (which answers before the client ends its half) is just another `IGrpcReferenceService` in
  `Grpc/Reflection/`. A call no service answers by the end of its request gets Trailers-Only `grpc-status 12`.
- Replies: `GrpcReferenceReply` holds the response headers (`:status` included, so a case picks any HTTP status), the
  DATA payloads exactly as split on the wire, the trailers (empty: an empty DATA frame ends the stream), or
  Trailers-Only; `Ok(...)` and `TrailersOnly(...)` build the common ones; `GrpcLengthPrefixedMessage.Frame` sets the
  Compressed-Flag.
- Added `Documentation/Conformance/Extended/Grpc.md` to touches (no task in Doing named it): the matrix check fails a
  carried `Grpc:` ID with no row, so it holds this case's seven rows (`Grpc:Http2-<section>-<n>`); BL-4533 completes it.
- Measured: the unary case passes as written (no known failure); `Measure-CodeQuality.ps1 -Library
  Curl.Conformance.Extended.UnitLibrary` reports 0 failing members after splitting `ReceiveAsync` (complexity 14).
  Full build clean, fast tests green. The tests are platform-neutral (temporary directory via
  `Directory.CreateTempSubdirectory`, no paths in assertions); CI on Linux and macOS is left to the shift's watch.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 846644c2 9a1cc775 d8e1cf5f 8ca147d6 3af8a194.
- 2026-11-02: Doing -> Done. A scripted gRPC reference server over HTTP/2 records calls, and a unary grpc:// case passes against it
