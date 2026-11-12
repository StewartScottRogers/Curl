# Grpc: gRPC over HTTP/2

Curl's `grpc://` and `grpcs://` client measured against grpc/grpc's "gRPC over HTTP2"
(<https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md>) and `doc/statuscodes.md`. IDs are
`Grpc:Http2-<section>-<n>`. The cases run against `GrpcReferenceServer` (`Curl.Conformance.Extended.UnitLibrary/Grpc/`),
which records each call's decoded request headers and DATA frames and sends the reply the case scripts.

This matrix holds the request and unary call rows (BL-4530, BL-4533) and the framing, `grpc-timeout` and `grpcs://` rows (BL-4532). The response rows are in [Grpc-Responses.md](Grpc-Responses.md), the transport (RST_STREAM, GOAWAY, PING) and response-value rows in [Grpc-Transport.md](Grpc-Transport.md), and server reflection and the proto3 JSON mapping in [Grpc-Reflection.md](Grpc-Reflection.md) (BL-4478). Together they give every MUST, MUST NOT, SHOULD and SHOULD NOT of PROTOCOL-HTTP2.md that binds a client a row; the requirements that bind only a server or an application are the `n/a` rows below.

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
| Grpc:Http2-Request-7 | MUST | With no Message-Encoding (`grpc-encoding`) header, every request Compressed-Flag is 0. | GrpcRequestHeaderCases.MessageEncoding_NotSent_EveryCompressedFlagIsZero |
| Grpc:Http2-Request-8 | MUST | Custom-Metadata goes as request headers: `-H "x-trace: abc"` reaches the server as `x-trace: abc`. | GrpcRequestHeaderCases.CustomMetadata_GivenWithH_IsSentAsARequestHeader |
| Grpc:Http2-Request-9 | MUST | A Binary-Header (key ending `-bin`) value is base64 encoded (ADR-1226: `-H` takes it as base64). | GrpcRequestHeaderCases.BinaryMetadata_GivenWithH_IsSentBase64Encoded |
| Grpc:Http2-Request-10 | SHOULD | A Binary-Header value is emitted un-padded. | GrpcRequestHeaderCases.BinaryMetadata_PaddedInput_IsSentUnpadded |
| Grpc:Http2-Request-11 | SHOULD | Timeout is sent immediately after the reserved (`:`) headers, and Call-Definition headers before Custom-Metadata. | GrpcRequestHeaderCases.HeaderOrder_TimeoutAndMetadata_TimeoutFollowsThePseudoHeadersAndMetadataComesLast |
| Grpc:Http2-Request-12 | SHOULD | A client provides a structured `user-agent` (the specification: "recommended"). | GrpcRequestHeaderCases.UserAgent_NotGiven_AUserAgentIsSent |
| Grpc:Http2-Request-13 | SHOULD | A server answers a Content-Type not starting `application/grpc` with HTTP 415. | n/a: binds the server |
| Grpc:Http2-Request-14 | SHOULD NOT | Applications do not use `grpc-` keys outside the specification as Custom-Metadata. | n/a: binds the application that names the metadata, not the client runtime |
| Grpc:Http2-Request-15 | MUST | A receiver accepts padded and un-padded Binary-Header values, and splits a received `-bin` value on `,` before decoding. | n/a: curl decodes no received binary metadata |
| Grpc:Http2-Response-7 | MUST | Status is sent in Trailers even when it is OK. | n/a: binds the server |
| Grpc:Http2-Response-8 | MUST NOT | `grpc-status-details-bin` does not contradict `grpc-status` and `grpc-message`, and its consumer verifies that. | n/a: curl does not decode status details |
| Grpc:Http2-Compression-1 | MUST | Compression contexts are not kept across message boundaries; each message gets a new one. | n/a: curl compresses no message |
| Grpc:Http2-GoAway-2 | SHOULD | A server sends GOAWAY before closing a connection. | n/a: binds the server |
| Grpc:Http2-Request-16 | n/a | `-T file` sends the file as the request message exactly as `-d` does, with or without `--grpc-proto`; `-d` wins when both are given (BL-5018). | GrpcProtocolHandlerTests.ExecuteAsync_UploadWithGrpcProto_SendsTheFileAsTheRequestMessage, GrpcProtocolHandlerTests.ExecuteAsync_UploadWithoutGrpcProto_SendsTheFileAsTheRequestMessageAfterReflection, GrpcProtocolHandlerTests.ExecuteAsync_DataAndUpload_SendsTheDataNotTheFile |
