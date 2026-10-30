# Curl.Extension.Grpc.UnitLibrary

The gRPC extension (ADR-0999, ADR-1133): calling a gRPC method over HTTP/2 as grpc.io's
PROTOCOL-HTTP2.md specifies. It sits in `/Extensions/Grpc/` in `Curl.slnx` and references only
`Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Extension.Abstractions.UnitLibrary`; the client
flow may add the shared `Curl.Http2.UnitLibrary` and `Curl.Protobuf.UnitLibrary`. It never
references a `Curl.Protocol.*` library or another extension.

Namespace `Curl.Extension.Grpc`, with `Wire` below it. `Wire/` is the wire format; it works on
header lists (`KeyValuePair<string, string>`, names lower case) and bytes, and owns no stream or
connection. The client flow (BL-4341) and server reflection (BL-4342) build on it, and BL-4341
registers the extension in `ExtensionCatalogue.cs`.

- `GrpcRequestHeaders` builds a call's Request-Headers for a `GrpcRequestTarget`, with
  `GrpcTimeout` writing `grpc-timeout` (finest unit that fits 8 digits, rounded up).
- `LengthPrefixedMessage` frames a message; `LengthPrefixedMessageReader` reads `GrpcMessage`s
  from body bytes in any pieces, and its `Complete` fails a truncated message.
- `GrpcResponseHeaders` reads the response headers (`GrpcResponseHead`: a Trailers-Only response,
  or an HTTP status other than 200 mapped to a gRPC code) and the trailers (`GrpcStatus`, with
  `GrpcStatusMessage` percent-decoding `grpc-message`). `GrpcStatus.CodeName` is the
  statuscodes.md name of its `GrpcStatusCode`.
- Everything a peer gets wrong is a `GrpcWireException`.
