# Curl.Extended.Protocol.Grpc.UnitLibrary

The gRPC extension (ADR-0999, ADR-1133, ADR-1147): calling a gRPC method over HTTP/2 as grpc.io's
PROTOCOL-HTTP2.md specifies. It sits in `/Extended/Protocols/Grpc/` in `Curl.slnx` and references
`Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Extension.Abstractions.UnitLibrary` and the shared
`Curl.Http2.UnitLibrary` and `Curl.Protobuf.UnitLibrary`. It never references a `Curl.Protocol.*`
library or another extension.

Namespace `Curl.Extended.Protocol.Grpc`, with `Wire`, `Client` and `Reflection` below it. `Wire/` is the wire format; it
works on header lists (`KeyValuePair<string, string>`, names lower case) and bytes, and owns no
stream or connection.

- `GrpcRequestHeaders` builds a call's Request-Headers for a `GrpcRequestTarget`, with
  `GrpcTimeout` writing `grpc-timeout` (finest unit that fits 8 digits, rounded up).
- `LengthPrefixedMessage` frames a message; `LengthPrefixedMessageReader` reads `GrpcMessage`s
  from body bytes in any pieces, and its `Complete` fails a truncated message.
- `GrpcResponseHeaders` reads the response headers (`GrpcResponseHead`: a Trailers-Only response,
  or an HTTP status other than 200 mapped to a gRPC code) and the trailers (`GrpcStatus`, with
  `GrpcStatusMessage` percent-decoding `grpc-message`). `GrpcStatus.CodeName` is the
  statuscodes.md name of its `GrpcStatusCode`.
- Everything a peer gets wrong is a `GrpcWireException`.

`Client/` (namespace `Curl.Extended.Protocol.Grpc.Client`, ADR-1147) makes the call:

- `GrpcExtension` is the description `curl --extended-help` lists, the `--grpc-proto` option, its hook
  and the handler factory, registered by one line each in `ExtensionCatalogue.cs`,
  `ExtensionDescriptionCatalogue.cs`, `ExtensionOptionCatalogue.cs` and
  `ExtensionTransferHookCatalogue.cs`.
- `GrpcProtoTransferHook` hands a `grpc://` or `grpcs://` transfer on as a `GrpcProtoContext`, which
  forwards the transfer's own context and carries the `.proto` file.
- `GrpcProtocolHandler` reads the service and method from the URL path (`GrpcMethodPath`), the
  method's message types from the file and its imports (`GrpcProtoMethod`, through
  `ProtoFileDirectoryReader`) or, without `--grpc-proto`, from the server (`Reflection/`), encodes `-d` and decodes each reply as JSON (`GrpcMessageJson`),
  connects through curl's connector (TLS with ALPN `h2` for `grpcs://`), makes the call with
  `GrpcHttp2Call` on a `GrpcHttp2Session` over a `ConnectionStream`, writes each message and ends by
  the `grpc-status`.
  `GrpcClientException` carries the exit code of everything that ends a call early.

`Reflection/` (namespace `Curl.Extended.Protocol.Grpc.Reflection`, ADR-1149) describes the method when no
`--grpc-proto` is given:

- `ServerReflectionClient` runs gRPC server reflection v1 on stream 1 of the call's own connection
  (`GrpcHttp2Session`, whose HPACK encoder and decoder the call on stream 3 shares): one
  `file_containing_symbol` for the service, one `file_by_filename` per dependency no file yet
  declares, then the method's types from the files.
- `ServerReflectionStream` is the bidirectional `ServerReflectionInfo` stream: one request, one
  response, then it ends its half and reads the trailers. `ServerReflectionRequest` writes the
  requests and `ServerReflectionResponse` reads the replies.
- `FileDescriptorProtoDecoder` turns a serialized `FileDescriptorProto` into a `ReflectedProtoFile`:
  its name, dependencies and the `ProtoFileDescriptor` the `.proto` parser would make (map entries
  as `map<,>` fields, proto3 `optional`, packing by syntax). `ReflectionMessageFields` reads a
  message's fields by number.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extended.Protocol.Grpc.UnitTests/Wire`, `Curl.Extended.Protocol.Grpc.UnitTests/Client` and
`Curl.Extended.Protocol.Grpc.UnitTests/Reflection`.
