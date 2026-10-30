# Curl.Extension.Grpc.UnitLibrary

The gRPC extension (ADR-0999, ADR-1133, ADR-1147): calling a gRPC method over HTTP/2 as grpc.io's
PROTOCOL-HTTP2.md specifies. It sits in `/Extensions/Grpc/` in `Curl.slnx` and references
`Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Extension.Abstractions.UnitLibrary` and the shared
`Curl.Http2.UnitLibrary` and `Curl.Protobuf.UnitLibrary`. It never references a `Curl.Protocol.*`
library or another extension.

Namespace `Curl.Extension.Grpc`, with `Wire` and `Client` below it. `Wire/` is the wire format; it
works on header lists (`KeyValuePair<string, string>`, names lower case) and bytes, and owns no
stream or connection. Server reflection (BL-4342) builds on both.

- `GrpcRequestHeaders` builds a call's Request-Headers for a `GrpcRequestTarget`, with
  `GrpcTimeout` writing `grpc-timeout` (finest unit that fits 8 digits, rounded up).
- `LengthPrefixedMessage` frames a message; `LengthPrefixedMessageReader` reads `GrpcMessage`s
  from body bytes in any pieces, and its `Complete` fails a truncated message.
- `GrpcResponseHeaders` reads the response headers (`GrpcResponseHead`: a Trailers-Only response,
  or an HTTP status other than 200 mapped to a gRPC code) and the trailers (`GrpcStatus`, with
  `GrpcStatusMessage` percent-decoding `grpc-message`). `GrpcStatus.CodeName` is the
  statuscodes.md name of its `GrpcStatusCode`.
- Everything a peer gets wrong is a `GrpcWireException`.

`Client/` (namespace `Curl.Extension.Grpc.Client`, ADR-1147) makes the call:

- `GrpcExtension` is the description `curl --extensions` lists, the `--grpc-proto` option, its hook
  and the handler factory, registered by one line each in `ExtensionCatalogue.cs`,
  `ExtensionDescriptionCatalogue.cs`, `ExtensionOptionCatalogue.cs` and
  `ExtensionTransferHookCatalogue.cs`.
- `GrpcProtoTransferHook` hands a `grpc://` or `grpcs://` transfer on as a `GrpcProtoContext`, which
  forwards the transfer's own context and carries the `.proto` file.
- `GrpcProtocolHandler` reads the service and method from the URL path (`GrpcMethodPath`), the
  method's message types from the file and its imports (`GrpcProtoMethod`, through
  `ProtoFileDirectoryReader`), encodes `-d` and decodes each reply as JSON (`GrpcMessageJson`),
  connects through curl's connector (TLS with ALPN `h2` for `grpcs://`), makes the call with
  `GrpcHttp2Call` over a `ConnectionStream`, writes each message and ends by the `grpc-status`.
  `GrpcClientException` carries the exit code of everything that ends a call early.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.Grpc.UnitTests/Wire` and `Curl.Extension.Grpc.UnitTests/Client`.
