# ADR-1232: ProtoJSON Any resolves its type URL in the message type's catalogue

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4720
- Decided by Claude under Stewart's delegation

## Context

protobuf's JSON mapping (<https://protobuf.dev/programming-guides/json/>) writes `google.protobuf.Any` as an object
holding `"@type"`, its type URL, beside the embedded message's own members, or beside `"value"` holding the embedded
message's JSON form when it is a well-known type with a special form. Converting either way needs the embedded type,
which the type URL names but no field of the Any type reaches. Conformance cases `Grpc:ProtoJson-Any-1` and
`Grpc:ProtoJson-Any-2` failed because `-d` JSON holding an Any was rejected.

## Decision

- Every `DynamicMessageType` carries the `DynamicTypeCatalogue` it came from (`Catalogue`), and the catalogue gains
  `TryGetMessageType`. The embedded type is the type URL's last path segment, looked up in the Any type's catalogue,
  so a gRPC call resolves it among the descriptors server reflection or `--proto` files loaded, without changing any
  caller.
- The well-known types written under `"value"` are Timestamp, Duration, the nine wrappers, FieldMask, Struct, Value,
  ListValue and Any itself; every other type, Empty included, puts its fields beside `"@type"`.
- A type URL without a `/`, or naming a type the catalogue does not hold, is `InvalidValue` when reading JSON, and an
  Any that does not resolve or whose value does not decode has no JSON form (`InvalidValue` from the converter), as
  protobuf's own JSON printers refuse an Any whose type they cannot find. An Any object without `"@type"` is accepted
  only when empty, and an empty Any is written as `{}`.
- Anys nested in Anys count toward `ProtobufReader.MaximumDepth` both ways, so hostile input cannot exhaust the stack.

## Consequences

The JSON mapping needs no type registry of its own: whatever files described the call describe its Anys.
