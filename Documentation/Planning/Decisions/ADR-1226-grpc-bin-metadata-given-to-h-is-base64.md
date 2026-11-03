# ADR-1226: A `-bin` metadata value given to `-H` on a gRPC call is read as base64

Status: Accepted. Date: 2026-11-03. Decided by Claude under Stewart's delegation (BL-4533).

## Context

PROTOCOL-HTTP2.md says a Binary-Header (a key ending `-bin`) carries arbitrary bytes, which implementations MUST base64
encode on the wire and SHOULD emit un-padded. A command line cannot carry arbitrary bytes portably, so `-H` needs a
rule for what the user writes after `trace-bin:`.

## Decision

`-H "<key>-bin: <value>"` on a `grpc://` or `grpcs://` call reads `<value>` as base64 (padded or not): the bytes it
encodes are the metadata. Curl sends them base64 encoded, un-padded. So `-H "trace-bin: AQI="` sends `trace-bin: AQI`.
This is what grpcurl does with `-H` for `-bin` keys, and grpc-go, which emits un-padded base64. Every other `-H` header
goes as Custom-Metadata unchanged, after the Call-Definition headers.

## Consequences

`GrpcRequestHeaderCases` pins it (`Grpc:Http2-Request-9`, `Grpc:Http2-Request-10` in
`Documentation/Conformance/Extended/Grpc.md`); BL-4674 implements it. A value that is not base64 is the fix task's to
decide, as curl's usage error.
