---
id: BL-4720
title: Fix Grpc conformance: ProtoJSON Any with @type (Grpc:ProtoJson-Any-1, Grpc:ProtoJson-Any-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4721]
touches: [Curl.Protobuf.UnitLibrary/Json, Curl.Protobuf.UnitLibrary/DynamicMessages/DynamicMessageType.cs, Curl.Protobuf.UnitLibrary/DynamicMessages/DynamicTypeCatalogue.cs, Curl.Protobuf.UnitTests/DynamicMessages/DynamicTypeCatalogueTests.cs, Curl.Protobuf.UnitTests/Json, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4720 — Fix Grpc conformance: ProtoJSON Any with @type (Grpc:ProtoJson-Any-1, Grpc:ProtoJson-Any-2)

## Goal

A `grpc://` call encodes and prints `google.protobuf.Any` as a JSON object holding `"@type"` beside the embedded
message's fields, or the embedded value's JSON form under `"value"` when it is a well-known type with a special form, as
protobuf's ProtoJSON format requires.

## Context

BL-4687 measured this in `Curl.Conformance.Extended.UnitTests/Grpc/Reflection/GrpcProtoJsonWellKnownCases.cs`
(`RoundTrip_AnyOfAMessage_IsTheTypeUrlBesideTheMessagesFields`, `RoundTrip_AnyOfAWellKnownType_PutsItsJsonFormInValue`):
curl rejects each `-d` (exit code not 0). The embedded type is the type URL's last path segment, resolved among the
descriptors reflection loaded. Depends on BL-4721: both add to `Curl.Protobuf.UnitLibrary/Json`, and an Any of a
wrapper or Struct needs that task's forms. Spec: <https://protobuf.dev/programming-guides/json/>.

## Acceptance criteria

- [x] The cases for Grpc:ProtoJson-Any-1 and Grpc:ProtoJson-Any-2 pass and their lines are gone from
      `KnownFailures.txt`; the known-failures check passes.
- [x] `Curl.Protobuf.UnitTests` has a focused unit test for each form (ADR-0482), and `Curl.Protobuf.UnitLibrary` keeps
      100% line and branch coverage.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

- Done directly rather than through the full /feature stages: a change within one library, decided in ADR-1232.
- Touches widened to Curl.Protobuf.UnitLibrary/DynamicMessages/DynamicMessageType.cs, DynamicTypeCatalogue.cs and
  Curl.Protobuf.UnitTests/DynamicMessages/DynamicTypeCatalogueTests.cs: Any needs its embedded type, which only the
  catalogue reaches, so each type now carries its catalogue and the catalogue gains TryGetMessageType. No task in Doing
  on origin/work/dark-factory named them.
- New `Json/ProtobufJsonAny.cs`; the parser and formatter route Any objects to it. Tests in ProtobufJsonAnyTests.
- BL-4721 had left its five Grpc:ProtoJson-Struct/Wrappers/FieldMask lines in KnownFailures.txt although they pass
  (KnownFailurePassedException); removed them here. `DnsQuery:RFC9250-4.1.1-2 BL-4720` names this task but is a DNS
  over QUIC case; left as it is, since it still fails and the known-failures check passes.
- Measured: Curl.Protobuf.UnitLibrary 100% line and branch, 0 failing members (worst CRAP 10); solution build with
  -warnaserror clean; fast tests green (Curl.Conformance.Extended.UnitTests 1717 passed). Changes are
  platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: c55e2f87 3578e965 e3a4fd8b.
- 2026-11-03: Doing -> Done. grpc:// encodes and prints google.protobuf.Any as @type beside the embedded fields, or beside value for a well-known type
