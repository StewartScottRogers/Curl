---
id: BL-4721
title: Fix Grpc conformance: ProtoJSON wrapper types, FieldMask and Struct/Value/ListValue forms (Grpc:ProtoJson-Wrappers-1, Grpc:ProtoJson-Wrappers-2, Grpc:ProtoJson-FieldMask-1, Grpc:ProtoJson-Struct-1, Grpc:ProtoJson-Struct-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protobuf.UnitLibrary/Json, Curl.Protobuf.UnitTests/Json, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4721 — Fix Grpc conformance: ProtoJSON wrapper types, FieldMask and Struct/Value/ListValue forms (Grpc:ProtoJson-Wrappers-1, Grpc:ProtoJson-Wrappers-2, Grpc:ProtoJson-FieldMask-1, Grpc:ProtoJson-Struct-1, Grpc:ProtoJson-Struct-2)

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

- [x] The cases for Grpc:ProtoJson-Wrappers-1, Grpc:ProtoJson-Wrappers-2, Grpc:ProtoJson-FieldMask-1,
      Grpc:ProtoJson-Struct-1 and Grpc:ProtoJson-Struct-2 pass and their lines are gone from `KnownFailures.txt`; the
      known-failures check passes.
- [x] `Curl.Protobuf.UnitTests` has a focused unit test for each form (ADR-0482), and `Curl.Protobuf.UnitLibrary` keeps
      100% line and branch coverage.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

- Plan (pipeline feature, done directly to keep the run inside its cost cap): three new files beside
  `ProtobufJsonTimeTypes.cs` - `ProtobufJsonWrapperTypes.cs`, `ProtobufJsonFieldMask.cs`, `ProtobufJsonStructTypes.cs` -
  called from `ProtobufJsonFormatter.WriteMessageValue` and `ProtobufJsonParser.TryReadMessageValue`.
- A wrapper is written as its `value` field (the default when unset), so a present zero is `0`; the encoder still writes
  the zero wrapper as an empty message, as the case expects.
- FieldMask: paths joined by `,`, `_x` to `X` on write and `X` to `_x` on read; an empty string is no paths.
- Struct/Value/ListValue nest at most `ProtobufReader.MaximumDepth` deep (NestingTooDeep), and a JSON number beyond a
  double (`1e400`) is InvalidValue. A Value with no kind set throws FormatException on write, as an out-of-range
  Timestamp does. A JSON `null` in a singular Value or NullValue field is a value, not "leave unset".
- Decision (sensible default): `ProtoEnumDescriptor` keeps only an enum's own name and `DynamicField` holds no enum full
  name (adding one is outside `touches`), so NullValue is recognised by its shape - named `NullValue` with the one value
  `NULL_VALUE`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary` 100% line, 100% branch, 0 failing members;
  `GrpcProtoJson*` conformance cases 21/21; `dotnet build Curl.slnx -warnaserror` clean; fast tests green (115 projects).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 655c552e 3578e965 e3a4fd8b.
- 2026-11-03: Doing -> Done. grpc:// ProtoJSON reads and writes the wrapper types, FieldMask and Struct/Value/ListValue/NullValue forms
