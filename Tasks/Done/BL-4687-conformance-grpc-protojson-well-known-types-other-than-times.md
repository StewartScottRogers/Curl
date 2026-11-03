---
id: BL-4687
title: Conformance: gRPC ProtoJSON well-known types other than Timestamp and Duration (Any, Struct, wrappers, FieldMask, Empty)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4686]
touches: [Curl.Conformance.Extended.UnitTests/Grpc/Reflection/GrpcProtoJsonWellKnownCases.cs, Documentation/Conformance/Extended/Grpc-Reflection.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4687 — Conformance: gRPC ProtoJSON well-known types other than Timestamp and Duration (Any, Struct, wrappers, FieldMask, Empty)

## Goal

Every ProtoJSON well-known type other than Timestamp and Duration (`Any`, `Struct`/`Value`/`ListValue`/`NullValue`,
the wrapper types, `FieldMask`, `Empty`) has a row in `Documentation/Conformance/Extended/Grpc-Reflection.md` mapped
to a case, in place of the `Grpc:ProtoJson-WellKnown-1` n/a row BL-4478 left.

## Context

BL-4478 measured the proto3 JSON mapping of a reflected call (`GrpcProtoJsonCases`, served by
`GrpcReflectionService` and `GrpcEchoService` in `Curl.Conformance.Extended.UnitLibrary/Grpc/Reflection/`, descriptors
built with `ReflectionDescriptors`) and found Timestamp and Duration unsupported (BL-4686). Write the remaining
well-known types' cases in a new `GrpcProtoJsonWellKnownCases.cs` the same way, against protobuf.dev's "ProtoJSON
Format"; a case that fails goes in `KnownFailures.txt` with a filed High `Fix Grpc conformance: ...` task.

## Acceptance criteria

- [x] `Grpc-Reflection.md` gives each of those types' JSON rules a `Grpc:ProtoJson-` row mapped to a case, and the
      `Grpc:ProtoJson-WellKnown-1` n/a row is gone; the matrix check passes.
- [x] Every failing case is listed in `KnownFailures.txt` with its filed High fix task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 74ac21ad 3006bacd e3a4fd8b.
- 2026-11-03: Cases written; 7 known failures filed as BL-4719 and BL-4720.
- 2026-11-03: Doing -> Done. Every ProtoJSON well-known type other than Timestamp and Duration has a Grpc:ProtoJson- row and case; the failures are listed for BL-4719 and BL-4720
