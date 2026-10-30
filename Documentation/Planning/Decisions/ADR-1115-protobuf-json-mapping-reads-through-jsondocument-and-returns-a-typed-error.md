# ADR-1115 — The Protocol Buffers JSON mapping reads through JsonDocument and returns a typed error

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4166.

## Context

The gRPC, gRPC-Web and Connect extensions take a request body as JSON and print a response as JSON, so
`Curl.Protobuf.UnitLibrary` converts JSON to Protocol Buffers binary and back for a message type known only
at run time (`DynamicMessageType`, ADR-1112), under the proto3 JSON mapping
(https://protobuf.dev/programming-guides/json/). The base class library has `System.Text.Json` but no
Protocol Buffers support, and the conversion must never throw for bad input.

## Decision

- **Reading.** `ProtobufJsonParser` parses with `JsonDocument` (built on `Utf8JsonReader`, AOT safe, no
  reflection serializer) with no document depth limit, and walks the elements into a `DynamicMessage`.
  The tree makes each rule one small method; a request body is small enough to hold whole.
- **Writing.** `ProtobufJsonFormatter` writes with `Utf8JsonWriter`, on one line or indented, with relaxed
  escaping so non-ASCII text is written as UTF-8, as `protoc` and `grpcurl` print it.
- **Errors.** Every conversion returns a `ProtobufJsonError`: `MalformedJson` (including invalid UTF-8 and
  lone escaped surrogates, which the document reader reports only when a string is read), `UnknownField`,
  `WrongType` (a JSON kind the field cannot hold), `InvalidValue` (right kind, not a value: out of range, not
  integral, bad base64, no such enum name), `NestingTooDeep` and, from binary, `MalformedBinary`.
- **Depth.** Messages nest at most `ProtobufReader.MaximumDepth` (100) below the top-level message, the same
  limit the binary decoder keeps, so JSON and binary accept the same messages. Arrays inside arrays are
  `WrongType` at the first level, so the walk's recursion is bounded by the message depth.
- **The mapping's choices.** Fields are matched by JSON name (the `json_name` option, else lowerCamelCase)
  or original name and written by JSON name. 64-bit integers are written as strings; integers are read from
  numbers or strings, `1.0` and `1e2` included. Floating point accepts `"NaN"`, `"Infinity"` and `"-Infinity"`
  and writes them so; a finite value past `float`'s range is `InvalidValue`. Bytes are written as standard
  padded base64 and read as standard or URL-safe, padded or not. Enums are written by name, or by number
  when the enum names none (proto3 enums are open), and read from either. A JSON `null` leaves the field
  unset. Fields without presence are left out at their default value, empty repeated and map fields always.
  Unknown fields read from binary are dropped, since JSON has no form for them.
- **Left for later.** The well-known types' special JSON forms (`Timestamp`, `Duration`, `Any`, wrappers,
  `Struct`) apply only once their `.proto` files are given; until a task adds them they map as ordinary
  messages. Two members of one oneof, or one field given twice, are not rejected: the last wins.

## Consequences

The extensions convert with `ProtobufJsonConverter.TryConvertJsonToBinary` and `TryConvertBinaryToJson`
and print the error they get; no JSON input can make the library throw.
