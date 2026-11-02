# Grpc: gRPC over HTTP/2

Curl's `grpc://` and `grpcs://` client measured against grpc/grpc's "gRPC over HTTP2"
(<https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md>) and `doc/statuscodes.md`. IDs are
`Grpc:Http2-<section>-<n>`. The cases run against `GrpcReferenceServer` (`Curl.Conformance.Extended.UnitLibrary/Grpc/`),
which records each call's decoded request headers and DATA frames and sends the reply the case scripts.

This matrix holds the unary call's rows (BL-4530) and the framing, `grpc-timeout` and `grpcs://` rows (BL-4532); BL-4533 completes it with every remaining client requirement.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Grpc:Http2-Request-1 | MUST | The request's `:method` is `POST`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-2 | MUST | The request's `:scheme` is `http` or `https`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-3 | MUST | The request's `:path` is `/` Service-Name `/` method name. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-4 | MUST | The request carries `te: trailers`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Request-5 | MUST | The request's `content-type` starts with `application/grpc`. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Message-1 | MUST | A message is a Length-Prefixed-Message: a Compressed-Flag byte, a four-byte big-endian Message-Length and the message. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Response-1 | MUST | A response of Response-Headers, messages and Trailers with `grpc-status 0` is a successful call. | GrpcUnaryCases.Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply |
| Grpc:Http2-Message-2 | MUST | A Length-Prefixed-Message may span DATA frames; the client reassembles it. | GrpcFramingCases.Framing_MessageSplitAcrossDataFrames_PrintsTheWholeMessage |
| Grpc:Http2-Message-3 | MUST | A response body that ends inside a Length-Prefixed-Message is not a successful call. | GrpcFramingCases.Framing_BodyEndsInsideAMessage_FailsWithWeirdServerReply |
| Grpc:Http2-Message-4 | MUST | A Compressed-Flag of 1 means the message is in the Message-Encoding; a client that asked for no encoding cannot decode it and fails the call. | GrpcFramingCases.Framing_CompressedFlagWithoutAnAcceptedEncoding_FailsWithBadContentEncoding |
| Grpc:Http2-Timeout-1 | MUST | `grpc-timeout` is a TimeoutValue of at most 8 ASCII digits and a TimeoutUnit (`H`, `M`, `S`, `m`, `u`, `n`). | GrpcTimeoutCases.Timeout_MaxTime_SendsGrpcTimeoutInTheFinestUnitWithinEightDigits |
| Grpc:Http2-Timeout-2 | MAY | Timeout is optional; omitted, the server assumes an infinite timeout. | GrpcTimeoutCases.Timeout_NoMaxTime_SendsNoGrpcTimeout |
| Grpc:Http2-Tls-1 | MUST | gRPC over TLS runs as HTTP/2 `h2`, negotiated with ALPN. | GrpcTlsCases.Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls |
