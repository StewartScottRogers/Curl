# Grpc: gRPC over HTTP/2 (part 2: responses)

Curl's `grpc://` client read against the "Responses" section of grpc/grpc's "gRPC over HTTP2"
(<https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md>), its `doc/statuscodes.md` and
`doc/http-grpc-status-mapping.md` (BL-4531). Part 1, the request and the unary call, is [Grpc.md](Grpc.md). The cases
are `GrpcResponseCases` and `GrpcStatusCases` in `Curl.Conformance.Extended.UnitTests/Grpc/`, run against
`GrpcReferenceServer` scripted with the reply each case needs. A call that ends with a non-OK status exits 22 and
writes `curl: (22) grpc-status <code> (<name>): <message>` to stderr.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Grpc:Http2-Response-2 | MUST | A response's Length-Prefixed-Messages are read one after another until the Trailers: a server-streaming reply prints every message, in order. | GrpcResponseCases.ServerStreaming_ThreeMessagesThenOk_PrintsEachMessageInOrder |
| Grpc:Http2-Response-3 | MUST | A Trailers-Only response, one HEADERS frame carrying `grpc-status` and ending the stream, ends the call by that status. | GrpcResponseCases.TrailersOnly_OkInTheOnlyHeaders_EndsTheCallWithNoOutput |
| Grpc:Http2-Response-4 | MUST | `grpc-message` is percent-decoded before it is shown. | GrpcResponseCases.GrpcMessage_PercentEncoded_IsReportedDecoded |
| Grpc:Http2-Response-5 | SHOULD | A non-200 HTTP status with no `grpc-status` ends the call with the code http-grpc-status-mapping.md gives it (UNKNOWN for a status it does not list). | GrpcResponseCases.HttpStatus_NotOkWithNoGrpcStatus_EndsTheCallWithItsMappedCode |
| Grpc:StatusCodes-Names-1 | MUST | Every status code is reported by the name statuscodes.md gives it. | GrpcStatusCases.NonOkStatus_InTrailersOnly_ExitsTwentyTwoNamingTheCode |
