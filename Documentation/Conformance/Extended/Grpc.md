# Grpc: gRPC over HTTP/2

Curl's `grpc://` and `grpcs://` client measured against grpc/grpc's "gRPC over HTTP2"
(<https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md>) and `doc/statuscodes.md`. IDs are
`Grpc:Http2-<section>-<n>`. The cases run against `GrpcReferenceServer` (`Curl.Conformance.Extended.UnitLibrary/Grpc/`),
which records each call's decoded request headers and DATA frames and sends the reply the case scripts.

This matrix holds the unary call's rows (BL-4530); BL-4533 completes it with every remaining client requirement.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Grpc:Http2-Request-1 | MUST | The request's `:method` is `POST`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-2 | MUST | The request's `:scheme` is `http` or `https`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-3 | MUST | The request's `:path` is `/` Service-Name `/` method name. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-4 | MUST | The request carries `te: trailers`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-5 | MUST | The request's `content-type` starts with `application/grpc`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Message-1 | MUST | A message is a Length-Prefixed-Message: a Compressed-Flag byte, a four-byte big-endian Message-Length and the message. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Response-1 | MUST | A response of Response-Headers, messages and Trailers with `grpc-status 0` is a successful call. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
