# ADR-1149: The gRPC extension describes the method by server reflection v1 without --grpc-proto

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4342
- Decided by Claude under Stewart's delegation

## Context

ADR-1147 made a `grpc://` or `grpcs://` call read the method's message types from a `--grpc-proto`
file and fail without one. BL-4342 fills that gap with gRPC server reflection v1:
`grpc.reflection.v1.ServerReflection/ServerReflectionInfo`, a bidirectional streaming method
(grpc/grpc-proto `grpc/reflection/v1/reflection.proto`) whose `file_containing_symbol` and
`file_by_filename` requests are answered with serialized `google.protobuf.FileDescriptorProto`s
(descriptor.proto). Upstream curl has no gRPC support, so the choices below follow grpc.io and what
gRPC's own command-line clients (grpcurl) do, not curl.

## Decision

- **When.** Only when `--grpc-proto` is absent; a given file always wins and no reflection is sent.
- **One connection.** Reflection runs on stream 1 of the call's own HTTP/2 connection and the call on
  stream 3, as grpcurl does; both streams share one HPACK encoder and decoder
  (`GrpcHttp2Session`), since HPACK's dynamic tables span the connection.
- **Requests.** One `file_containing_symbol` with the URL's service name, then one `file_by_filename`
  for each dependency that no file received so far declares, one request at a time on the one
  stream, in the order the files list them. Servers that send a file's dependencies with it
  (grpc-go does) are asked for nothing more. Then the client ends its half of the stream and reads
  the server's to its trailers, whatever their status, so the call that follows has the connection
  alone. `host` is not sent.
- **Decoding.** Each `FileDescriptorProto` becomes the `ProtoFileDescriptor` the `.proto` parser
  would make: type names stay fully qualified with a leading dot; a nested message with
  `map_entry` becomes a `map<,>` field; a field in a real oneof is singular, and a proto3
  `optional` field (its synthetic oneof) or any field of a file that is not proto3 (proto2,
  editions) has presence; a repeated scalar is packed unless `FieldOptions.packed` says otherwise
  or the file is proto2 without it. A group field (type 10) cannot be carried and fails.
- **Failures.** A server without reflection (any `grpc-status` before an answer, typically 12
  UNIMPLEMENTED) or an `error_response` is exit 22, as any other `grpc-status` from the server
  (ADR-1147), and the message says to give the method's `.proto` file with `--grpc-proto`. Files that
  declare no such service or method are exit 2, as with `--grpc-proto`. Bytes that do not decode, a
  `file_by_filename` reply without that file, or types that do not resolve are exit 8 (weird server
  reply). A closed connection or a flow-control window that does not take a request is exit 16.

## Consequences

- `curl grpc://host:port/pkg.Service/Method -d '{...}'` works against any server with reflection
  enabled, with no file on disk.
- Reflection costs one round trip per missing dependency before the call.
- v1alpha (`grpc.reflection.v1alpha`) is not tried as a fallback; a server offering only v1alpha fails
  with the message naming `--grpc-proto`. A task can add the fallback if a server needs it.
