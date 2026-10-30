# ADR-1106: Dynamic Protocol Buffers messages hold boxed values by field and decode to the wire codec's errors

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4169
- Decided by Claude under Stewart's delegation.

## Context

`Curl.Protobuf.UnitLibrary` had the wire codec (BL-4164) and `.proto` descriptors whose named types
are still names as written (BL-4170). The gRPC, gRPC-Web and Connect extensions, and the JSON mapping
(BL-4166), need a message of a type known only at run time encoded to and decoded from binary, per
https://protobuf.dev/programming-guides/encoding/. There is no generated code and no reflection (native AOT).

## Decision

- `DynamicMessages/`: `DynamicTypeCatalogue` indexes the messages and enums of a set of files by full
  name and makes `DynamicMessageType`s whose `DynamicField`s have their types resolved, innermost scope
  out (`Inner` in `pkg.Outer` is tried as `pkg.Outer.Inner`, `pkg.Inner`, then `Inner`; a leading `.`
  is absolute). A name it cannot resolve is an `ArgumentException` when the type is made, never while
  decoding. It is thread safe, and a type whose fields do not all resolve is not kept.
- A `map<K, V>` field is a repeated message field whose entry type has `key` = 1 and `value` = 2, both
  with explicit presence so both are always written, as protoc's runtimes do.
- `DynamicMessage` holds each value boxed as the .NET type its `DynamicValueKind` names (`int` for
  `int32`, `sint32`, `sfixed32` and enums; `uint` for `uint32` and `fixed32`; `byte[]` for `bytes`;
  `DynamicMessage` for messages), repeated fields as `IList<object>` and maps as
  `IDictionary<object, object>`. `SetValue` checks the type; values added to a list or map are checked
  only when encoding casts them. Enums are open: a number the enum does not name is kept.
- Encoding writes known fields in field number order, then unknown fields as read. A singular field
  without presence is skipped when its raw wire value is 0 (or it is empty), so `-0.0` is still
  written, as protoc does. `optional`, oneof and message fields are written whenever set. Repeated
  numeric fields are packed unless `[packed = false]`.
- Decoding accepts packed and unpacked repeated numeric fields; the last singular value and the last
  oneof field win; a message field read twice is merged. A field the type does not have, or that comes
  with another wire type than its field's, is kept as an unknown field (as protoc's C++ runtime does).
- Malformed input is the wire codec's `ProtobufDecodeError`. Proto3 rejects a `string` that is not
  UTF-8, so `ProtobufDecodeError` gains `InvalidUtf8`; and `ProtobufReader` gains a public
  `TryReadFieldValue`, which reads the value of a field whose tag was already read, so a field found
  unknown after its tag is kept and nested messages keep the reader's depth limit.

## Consequences

The JSON mapping and the RPC extensions build on `DynamicMessage` without generated code. Groups
(proto2) are only ever unknown fields, kept and written back whole.
