# ADR-1228: ProtoJSON writes and reads `google.protobuf.Timestamp` and `Duration` as strings

Status: Accepted. Date: 2026-11-03. Decided by Claude under Stewart's delegation (BL-4686).

## Context

protobuf.dev/programming-guides/json maps the well-known types `google.protobuf.Timestamp` to an RFC 3339 string
and `google.protobuf.Duration` to a seconds string ending in "s". `Curl.Protobuf`'s JSON mapping (ADR-1147) wrote
both as plain `{seconds, nanos}` messages and rejected their string forms, so a gRPC call with
`{"at":"1970-01-01T00:00:01Z"}` exited 2 (`Grpc:ProtoJson-Timestamp-1`, `Grpc:ProtoJson-Duration-1`). curl has no
gRPC, so there is no curl output to measure; the rules follow the specification and protobuf's own printer.

## Decision

- A message type is recognised by its full name alone, so descriptors from server reflection and from a
  `--grpc-proto` file are treated alike (`ProtobufJsonTimeTypes`, `Curl.Protobuf.UnitLibrary/Json/`).
- Output: a Timestamp is written in UTC with "Z"; both types have 0, 3, 6 or 9 fractional digits, the fewest that
  hold the nanoseconds exactly (`1.500s`, `0.000001s`). A negative Duration is written with one leading "-".
- Input: a Timestamp is `yyyy-MM-ddTHH:mm:ss`, an optional "." and 1 to 9 digits, then "Z" or an offset `+HH:mm`
  or `-HH:mm`, between 0001-01-01T00:00:00Z and 9999-12-31T23:59:59.999999999Z. A Duration is an optional "-",
  at most 315,576,000,000 seconds, an optional "." and 1 to 9 digits, then "s". Any other string is
  `InvalidValue`, any other JSON kind `WrongType`.
- A value outside its type's range (from the wire) has no JSON form: `ProtobufJsonFormatter.Format` throws
  `FormatException`, which `ProtobufJsonConverter.TryConvertBinaryToJson` reports as `InvalidValue`, as protobuf's
  printer fails rather than printing a wrong time.
- Parsing is by hand with `DateTime.TryParseExact` and `TimeSpan.TryParseExact`, not a source-generated regex,
  whose generated code would fall under the library's coverage and complexity gates.

## Consequences

A `grpc://` call (the only user of `ProtobufJsonConverter`) reads and prints Timestamp and Duration fields as their ProtoJSON strings. The
other well-known types were left alone by this decision. Later decisions and code gave most of them their own ProtoJSON
forms: the nine wrapper types (`ProtobufJsonWrapperTypes.cs`), `Struct`, `Value` and `ListValue` (`ProtobufJsonStructTypes.cs`),
`FieldMask` (`ProtobufJsonFieldMask.cs`) and `Any` (`ProtobufJsonAny.cs`, ADR-1232). Only `Empty` keeps its plain message form.
