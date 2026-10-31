# ADR-1133: The gRPC extension's wire format follows grpc.io's PROTOCOL-HTTP2.md

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4340
- Decided by Claude under Stewart's delegation

## Context

The gRPC extension (ADR-0999) calls a gRPC method over HTTP/2. BL-4102 was split into the wire
format (BL-4340), the client flow (BL-4341) and server reflection (BL-4342). Upstream curl has no
gRPC support, so the library is tested against grpc.io's PROTOCOL-HTTP2.md, statuscodes.md and
http-grpc-status-mapping.md, not against curl. Those documents leave a few choices to the client.

## Decision

- `Curl.Extension.Protocol.Grpc.UnitLibrary` references only `Curl.Protocol.Abstractions` and
  `Curl.Extension.Abstractions` for now; `Wire/` works on header lists
  (`KeyValuePair<string, string>`) and bytes, so it needs neither HPACK nor protobuf. The client
  flow adds `Curl.Http2` and `Curl.Protobuf` when it needs them. Its types are internal.
- Request headers go in the order PROTOCOL-HTTP2.md's Call-Definition lists them, with
  `:authority` among the pseudo-headers since HTTP/2 puts every pseudo-header first.
  `user-agent` is the caller's (curl's own), and is left out when none is given.
- `grpc-timeout` uses the finest unit whose value fits the 8 digits allowed, rounding up, so the
  server never sees a shorter deadline than the client asked for; past 99999999 hours it is
  that maximum. A zero timeout is `0n`; a negative one is refused.
- Length-Prefixed-Message reading accepts a message split across any reads and several in one;
  a Compressed-Flag other than 0 or 1, a Message-Length past the maximum (4 MiB by default,
  gRPC's own clients' receive limit) or a body that ends inside a message is a
  `GrpcWireException`.
- Response headers: a `grpc-status` in the first HEADERS makes it Trailers-Only and ends the
  call. Without one, an HTTP status other than 200 ends the call with the code
  http-grpc-status-mapping.md gives it (UNKNOWN for statuses it does not list). A 200 response
  must be `application/grpc`, alone or followed by `+format` or `;` parameters, or it is a
  `GrpcWireException`.
- Trailers without `grpc-status` are a `GrpcWireException`; a `grpc-status` that is not a number
  is INTERNAL, as gRPC's own clients treat it. A code statuscodes.md does not define keeps its
  number, and its name is that number.
- `grpc-message` is percent-decoded as UTF-8 and never fails, as PROTOCOL-HTTP2.md requires: a
  `%` not followed by two hexadecimal digits is kept, and bytes that are not UTF-8 become U+FFFD.

## Consequences

The client flow (BL-4341) turns `GrpcStatus` into curl's output and exit code and registers the
extension; nothing in `Wire/` decides either.
