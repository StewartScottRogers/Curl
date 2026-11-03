---
id: BL-4720
title: Fix Grpc conformance: ProtoJSON Any with @type (Grpc:ProtoJson-Any-1, Grpc:ProtoJson-Any-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4719]
touches: [Curl.Protobuf.UnitLibrary/Json, Curl.Protobuf.UnitTests/Json, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
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
descriptors reflection loaded. Depends on BL-4719: both add to `Curl.Protobuf.UnitLibrary/Json`, and an Any of a
wrapper or Struct needs that task's forms. Spec: <https://protobuf.dev/programming-guides/json/>.

## Acceptance criteria

- [ ] The cases for Grpc:ProtoJson-Any-1 and Grpc:ProtoJson-Any-2 pass and their lines are gone from
      `KnownFailures.txt`; the known-failures check passes.
- [ ] `Curl.Protobuf.UnitTests` has a focused unit test for each form (ADR-0482), and `Curl.Protobuf.UnitLibrary` keeps
      100% line and branch coverage.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-03: Created.
