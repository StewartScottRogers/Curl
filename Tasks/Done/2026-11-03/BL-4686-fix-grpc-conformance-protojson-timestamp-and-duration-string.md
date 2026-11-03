---
id: BL-4686
title: Fix Grpc conformance: ProtoJSON Timestamp and Duration string forms (Grpc:ProtoJson-Timestamp-1, Grpc:ProtoJson-Duration-1)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protobuf.UnitLibrary/Json, Curl.Protobuf.UnitTests, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcMessageJson.cs, Curl.Extended.Protocol.Grpc.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4686 — Fix Grpc conformance: ProtoJSON Timestamp and Duration string forms (Grpc:ProtoJson-Timestamp-1, Grpc:ProtoJson-Duration-1)

## Goal

A `grpc://` call prints a `google.protobuf.Timestamp` field as an RFC 3339 string and a `google.protobuf.Duration` as
a seconds string with 0, 3, 6 or 9 fractional digits, and parses both forms from `-d`, as protobuf's ProtoJSON format
requires.

## Context

Found by BL-4478: `GrpcProtoJsonCases.RoundTrip_TimestampAndDuration_UseTheirStringForms`
(`Curl.Conformance.Extended.UnitTests/Grpc/Reflection/`) sends `{"at":"1970-01-01T00:00:01Z","took":"1.5s"}` to a
method whose types reach the client by server reflection, and curl exits 2: `Curl.Protobuf.UnitLibrary`'s JSON
converter (`Json/`) treats the two well-known types as plain messages `{seconds, nanos}`. Specification:
protobuf.dev/programming-guides/json ("Timestamp": RFC 3339, Z-normalized output, offsets other than Z accepted;
"Duration": generated output always 0, 3, 6 or 9 fractional digits followed by "s"). Recognise the types by full
name (`google.protobuf.Timestamp`, `google.protobuf.Duration`), whether they come from reflection or a `--grpc-proto`
import. ADR-1147 describes the JSON mapping.

## Acceptance criteria

- [x] `GrpcProtoJsonCases.RoundTrip_TimestampAndDuration_UseTheirStringForms` passes and the `Grpc:ProtoJson-Timestamp-1`
      and `Grpc:ProtoJson-Duration-1` lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] A focused unit test in `Curl.Protobuf.UnitTests` pins each conversion both ways (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the changed library stays at 100%
      line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Delivered directly rather than through the full /feature stages: one new internal class in `Curl.Protobuf.UnitLibrary/Json/` (`ProtobufJsonTimeTypes`) hooked into the formatter and parser; design recorded in ADR-1228. `GrpcMessageJson.cs` needed no change.
- Out-of-range Timestamp or Duration from the wire: `TryConvertBinaryToJson` returns `InvalidValue` (protobuf's printer fails too).
- A source-generated regex was tried first and dropped: its generated code counted against the coverage and complexity gates.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary` reports 100% line, 100% branch, 0 failing members, max CRAP 10. Fast tests: 115 projects green, Curl.Protobuf.UnitTests 385. Pure computation, no platform-dependent paths or error text, so CI on Linux and macOS is unaffected.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 523465ad b294690c 710af73a.
- 2026-11-03: Doing -> Done. grpc:// reads and prints Timestamp and Duration fields as their ProtoJSON strings
