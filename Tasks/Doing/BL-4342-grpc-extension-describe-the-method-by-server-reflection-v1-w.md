---
id: BL-4342
title: gRPC extension: describe the method by server reflection v1 when no .proto file is given
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4340, BL-4341]
touches: [Curl.Extension.Grpc.UnitLibrary/Reflection, Curl.Extension.Grpc.UnitTests/Reflection]
requirement: none
created: 2026-10-29
completed:
---
# BL-4342 — gRPC extension: describe the method by server reflection v1 when no .proto file is given


## Goal

A `grpc://` or `grpcs://` call without `--proto` learns the method's input and output types from the server by gRPC server reflection v1 and then makes the call as with a `.proto` file.

## Context

Third of three parts of BL-4102, on BL-4340's `Wire/` and BL-4341's `Client/`. Specification: `grpc.reflection.v1.ServerReflection/ServerReflectionInfo` (grpc/grpc-proto `reflection.proto`, v1): `file_containing_symbol` returns serialized `FileDescriptorProto`s, which must be decoded (with their dependencies) into `Curl.Protobuf.UnitLibrary` descriptors. Code goes in `Reflection/`; BL-4341's handler calls it where it now fails without `--proto`. If that call site in `Client/` must change, add the file to touches only when BL-4341 is Done.

## Acceptance criteria

- [ ] Without `--proto`, the client sends a `ServerReflectionRequest` with `file_containing_symbol` for the service, follows `dependency` names with `file_by_filename`, and builds the method's descriptors from the returned `FileDescriptorProto`s; unit tests over recorded bytes.
- [ ] An `error_response` or a server without reflection (`grpc-status` 12 UNIMPLEMENTED) fails with a message naming `--proto`; unit test.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 955021ab 07666595 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Backlog. Lane 13 claimed it as the shift ended (time up, 02:48); its worktree holds no work, so the next shift refused to start with it orphaned in Doing
- 2026-10-30: Backlog -> Doing. Criteria at claim: 955021ab 07666595 e35aff77 3d836d5e.
