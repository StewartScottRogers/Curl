# ADR-1005: The Protocol Buffers wire codec reports malformed input as a typed error

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4164
- Decided by Claude under Stewart's delegation.

## Context

The gRPC, gRPC-Web and Connect extensions (ADR-0999) need Protocol Buffers. The base
class library has none, packages are not allowed, and extensions may not reference one
another, so the codec is a shared Wire layer, `Curl.Protobuf.UnitLibrary`, as
`Curl.Zstandard.UnitLibrary` is. Its input arrives from servers and must not throw.

## Decision

- `ProtobufReader` reads with `TryRead...` methods returning a `ProtobufDecodeError`
  (`None` on success): truncated varint, varint longer than 10 bytes, truncated fixed
  value, length past end, wire type 6 or 7, field number 0 or above 2^29 - 1, unmatched
  end-group, unterminated group, and nesting deeper than 100.
- A varint's tenth byte may carry bits past the 64th; they are discarded, as the
  encoding specification says. Only a continuation bit on the tenth byte is an error.
- Nesting is limited to 100 levels of groups or embedded messages, the reference
  implementation's default recursion limit: a reader at depth 100 reads its own fields
  but refuses to open another level.
- Unknown fields are kept as `ProtobufField` records (groups with their inner fields) and
  written back byte for byte by `ProtobufWriter.WriteField`.
- `ProtobufWriter` throws only for programming errors (field number out of range, an
  end-group field), never for data.

## Consequences

`.proto` parsing (BL-4165) and the JSON mapping (BL-4166) build on these two types, in
`Descriptors/` and `Json/` beside `Wire/`.
