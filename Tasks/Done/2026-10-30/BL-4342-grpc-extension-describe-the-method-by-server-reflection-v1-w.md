---
id: BL-4342
title: gRPC extension: describe the method by server reflection v1 when no .proto file is given
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4340, BL-4341]
touches: [Curl.Extension.Grpc.UnitLibrary/Reflection, Curl.Extension.Grpc.UnitTests/Reflection, Curl.Extension.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extension.Grpc.UnitLibrary/Client/GrpcHttp2Call.cs, Curl.Extension.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Extension.Grpc.UnitLibrary/Client/GrpcHttp2Session.cs, Curl.Extension.Grpc.UnitLibrary/Client/GrpcExtension.cs, Curl.Extension.Grpc.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1147-grpc-extension-calls-a-method-over-http2-with-a-grpc-proto-file.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4342 — gRPC extension: describe the method by server reflection v1 when no .proto file is given


## Goal

A `grpc://` or `grpcs://` call without `--proto` learns the method's input and output types from the server by gRPC server reflection v1 and then makes the call as with a `.proto` file.

## Context

Third of three parts of BL-4102, on BL-4340's `Wire/` and BL-4341's `Client/`. Specification: `grpc.reflection.v1.ServerReflection/ServerReflectionInfo` (grpc/grpc-proto `reflection.proto`, v1): `file_containing_symbol` returns serialized `FileDescriptorProto`s, which must be decoded (with their dependencies) into `Curl.Protobuf.UnitLibrary` descriptors. Code goes in `Reflection/`; BL-4341's handler calls it where it now fails without `--proto`. If that call site in `Client/` must change, add the file to touches only when BL-4341 is Done.

## Acceptance criteria

- [x] Without `--proto`, the client sends a `ServerReflectionRequest` with `file_containing_symbol` for the service, follows `dependency` names with `file_by_filename`, and builds the method's descriptors from the returned `FileDescriptorProto`s; unit tests over recorded bytes.
- [x] An `error_response` or a server without reflection (`grpc-status` 12 UNIMPLEMENTED) fails with a message naming `--proto`; unit test.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-1149): without `--grpc-proto`, `GrpcProtocolHandler` connects, opens a `GrpcHttp2Session` (one HTTP/2 connection with one HPACK encoder and decoder, since HPACK tables span every stream) and runs `ServerReflectionClient` on stream 1: `file_containing_symbol` for the service, then `file_by_filename` for each dependency no received file declares, then ends its half and reads the trailers; the call follows on stream 3. `FileDescriptorProtoDecoder` turns each `FileDescriptorProto` into a `ProtoFileDescriptor` (leading-dot type names, map entries as `map<,>`, proto3 `optional`, packing by syntax), and `DynamicTypeCatalogue` builds the types.
- The option is `--grpc-proto` (ADR-1147), not `--proto`, which is curl's own; the failure messages name `--grpc-proto`.
- Exit codes (decided): no reflection on the server (any `grpc-status` before an answer, e.g. 12 UNIMPLEMENTED) and `error_response` are exit 22 like any server `grpc-status`; no such service or method is exit 2 as with a file; undecodable or unresolvable files are exit 8; a closed connection or a full flow-control window is exit 16.
- Touches widened (no other task in Doing on origin/work/dark-factory names any gRPC path; BL-4341 is Done): the call site `Client/GrpcProtocolHandler.cs`, `Client/GrpcHttp2Call.cs` (takes the shared session; its `ResponseReader` takes the connection's decoder), new `Client/GrpcHttp2Session.cs`, `Client/GrpcExtension.cs` (option and description text now mention reflection), the project `CLAUDE.md` and ADR-1147 (its "fails until BL-4342" line), plus the handler's test file.
- Recorded bytes: no protoc, grpcurl or reflection server is on this machine, so the tests write `FileDescriptorProto`s and `ServerReflectionResponse`s field by field in protoc's and grpc-go's order (`ReflectionFixture`), helloworld.proto among them, and replay them through a scripted connection.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.Grpc.UnitLibrary` - 100% line, 100% branch, max complexity 10, no failing member. A method-group `Select(DecodeEnum)` used at two sites shares one cached delegate, which left one cache branch unseen; a lambda fixed it.
- Platform: the tests use no file system, path, OS error or certificate; they run over in-memory bytes, so they are platform-neutral. Full `dotnet build` clean; fast tests green (110 test assemblies passed).
- Carried over from lane 13's stash db0d8085 (its run was killed at shift end with the work done); its ADR-1148 was taken by BL-4339 meanwhile, so it is ADR-1149 here.

## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 955021ab 07666595 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Backlog. Lane 13 claimed it as the shift ended (time up, 02:48); its worktree holds no work, so the next shift refused to start with it orphaned in Doing
- 2026-10-30: Backlog -> Doing. Criteria at claim: 955021ab 07666595 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. grpc:// and grpcs:// calls without --grpc-proto learn the method's types by gRPC server reflection v1 on the call's own connection
