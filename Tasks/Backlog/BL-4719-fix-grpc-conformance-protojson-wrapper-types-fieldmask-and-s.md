---
id: BL-4719
title: Fix Grpc conformance: ProtoJSON wrapper types, FieldMask and Struct/Value/ListValue forms (Grpc:ProtoJson-Wrappers-1, Grpc:ProtoJson-Wrappers-2, Grpc:ProtoJson-FieldMask-1, Grpc:ProtoJson-Struct-1, Grpc:ProtoJson-Struct-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protobuf.UnitLibrary/Json, Curl.Protobuf.UnitTests/Json, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4719 — Fix Grpc conformance: ProtoJSON wrapper types, FieldMask and Struct/Value/ListValue forms (Grpc:ProtoJson-Wrappers-1, Grpc:ProtoJson-Wrappers-2, Grpc:ProtoJson-FieldMask-1, Grpc:ProtoJson-Struct-1, Grpc:ProtoJson-Struct-2)

## Goal

A `grpc://` call encodes and prints the nine wrapper types as their wrapped JSON values (a present zero kept),
`google.protobuf.FieldMask` as one comma-separated string of lowerCamelCase paths, and `Struct`/`Value`/`ListValue`/
`NullValue` as any JSON object, value, array and `null`, as protobuf's ProtoJSON format requires.

## Context

BL-4687 measured these in `Curl.Conformance.Extended.UnitTests/Grpc/Reflection/GrpcProtoJsonWellKnownCases.cs`
(`RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero`, `RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths`,
`RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray`): curl rejects each `-d` (exit code not 0). The
well-known types' JSON forms live beside `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonTimeTypes.cs` (BL-4686); add one
file per type group there, called from `ProtobufJsonFormatter` and `ProtobufJsonParser`. Spec:
<https://protobuf.dev/programming-guides/json/>.

## Acceptance criteria

- [ ] The cases for Grpc:ProtoJson-Wrappers-1, Grpc:ProtoJson-Wrappers-2, Grpc:ProtoJson-FieldMask-1,
      Grpc:ProtoJson-Struct-1 and Grpc:ProtoJson-Struct-2 pass and their lines are gone from `KnownFailures.txt`; the
      known-failures check passes.
- [ ] `Curl.Protobuf.UnitTests` has a focused unit test for each form (ADR-0482), and `Curl.Protobuf.UnitLibrary` keeps
      100% line and branch coverage.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-03: Created.
