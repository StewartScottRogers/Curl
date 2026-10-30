# Curl.Extension.GrpcWeb.UnitLibrary

The `--grpc-web` extension (ADR-0999, ADR-1127): one unary gRPC-Web call over HTTP/1.1 or HTTP/2. Namespace
`Curl.Extension.GrpcWeb`. Tested against the gRPC-Web protocol (grpc.io PROTOCOL-WEB.md), not against curl.

- `GrpcWebExtension` holds the `--grpc-web` `ExtensionOption`, the `ExtensionDescription` and `HookFor`, the transfer
  hook activator; `Curl.Extension.Composition`'s catalogues register each with one line.
- `GrpcWebFraming` frames the request message and reads the reply's frames into a `GrpcWebReply`: its messages and
  the trailers frame's fields, which `GrpcWebTrailers` reads.
- `GrpcWebStatus` checks the call's `grpc-status`: 0 leaves the transfer, another fails it with exit 22, none with 8.
- `GrpcWebTransferHook` sends the `-d` data as an `application/grpc-web+proto` POST through `GrpcWebRequestContext`
  and writes the messages of a 200 gRPC-Web reply without their frames; any other reply as curl received it. A
  transfer that is not HTTP runs unchanged.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.GrpcWeb.UnitTests`.
