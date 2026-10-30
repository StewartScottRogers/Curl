# ADR-1147: The gRPC extension calls a method over HTTP/2 with a --grpc-proto file

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4341
- Decided by Claude under Stewart's delegation

## Context

BL-4341 is the client flow of the gRPC extension (ADR-0999), on BL-4340's wire format (ADR-1133).
Upstream curl has no gRPC support, so it follows grpc.io's documents, not curl: PROTOCOL-HTTP2.md,
statuscodes.md and http-grpc-status-mapping.md as they stand on the grpc/grpc master branch in
October 2026, and the proto3 JSON mapping as `Curl.Protobuf` implements it. Those documents leave the
command line, the URL form and the exit codes to the client.

## Decision

- **Schemes.** `grpc://` calls over cleartext HTTP/2 with prior knowledge (no Upgrade, as gRPC's own
  clients do) and `grpcs://` over TLS with ALPN `h2`; a `grpcs://` server that does not negotiate `h2`
  fails with exit 16. Both go through curl's own connector (`ProtocolHandlerServices.Connector`),
  which runs TLS through the TLS provider under curl's trust settings, so `-k`, `--cacert` and the
  rest apply. Default ports: 80 and 443, as for `http://` and `https://`; gRPC defines none.
- **URL path.** `grpc://host[:port]/package.Service/Method`: the path is PROTOCOL-HTTP2.md's
  `:path`, `/` Service-Name `/` method name, sent unchanged. Any other path is exit 3.
- **Option.** The `.proto` file is `--grpc-proto <file>`, not `--proto`: `--proto` is curl's own
  option (the protocols a transfer may use), and an extension never changes curl's behaviour.
  Imports are read from the file's own directory. A file that is missing or does not parse is exit
  26 (curl's read error, as for a missing `-d @file`); a service or method the files do not declare
  is exit 2. Without `--grpc-proto` the method is described by server reflection (ADR-1149, BL-4342).
- **Request.** `-d` is the request message as proto3 JSON, encoded by the method's input type; no
  `-d`, or an empty one, sends the empty message, and JSON that is not of the type is exit 2. One
  uncompressed Length-Prefixed-Message ends the stream, sent as the flow-control windows allow.
  `--max-time` becomes `grpc-timeout`. No `user-agent` is sent. A streaming method is called the
  same way: one request message, and every response message is written.
- **Response.** Each response message is written as JSON indented by two spaces with `\n` line
  ends on every platform, then a newline. A compressed message (Curl sends no
  `grpc-accept-encoding`) is exit 61; a message that is not of the output type, or any reply
  PROTOCOL-HTTP2.md does not allow (ADR-1133's `GrpcWireException`), is exit 8; an HTTP/2 failure
  (reset stream, GOAWAY, a protocol error, bad HPACK, a connection that ends before the trailers) is
  exit 16.
- **Status.** `grpc-status` 0 is exit 0. Any other code - from the trailers, a Trailers-Only
  response, or an HTTP status other than 200 mapped by http-grpc-status-mapping.md - is exit 22,
  curl's code for a server's error answer and the one `--grpc-web` uses (ADR-1132), with
  `curl: (22) grpc-status N (NAME): <grpc-message percent-decoded>` on stderr. Messages that came
  before the status are still written.

## Consequences

`curl --extensions` and `curl --ai-help extensions` list `grpc` with `--grpc-proto`. With
`CURL_EXTENSIONS=0` the schemes are unknown and fail as curl fails them (exit 1, `Protocol "grpc"
not supported`). Server reflection (BL-4342) fills in the call without `--grpc-proto`.
