# Grpc: gRPC over HTTP/2 (part 3: transport and response values)

Curl's `grpc://` client read against the "Errors", "Connection Management" (GOAWAY and PING) and "Responses" sections
and the Request-Headers ASCII-Value rule of grpc/grpc's "gRPC over HTTP2"
(<https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md>) (BL-4585). Part 1, the request and the unary call,
is [Grpc.md](Grpc.md); part 2, the responses, is [Grpc-Responses.md](Grpc-Responses.md). The cases are
`GrpcTransportCases` and `GrpcResponseValueCases` in `Curl.Conformance.Extended.UnitTests/Grpc/`, run against
`GrpcReferenceServer` scripted to send an RST_STREAM, a GOAWAY or a PING of its own (`GrpcReferenceReply.Reset`,
`GrpcReferenceReply.GoAway`, `GrpcReferenceReply.PingBeforeReply`). A call the transport ends is reported like a call
that ends with a non-OK status: exit 22 and `curl: (22) grpc-status <code> (<name>): <message>` (ADR-1202).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Grpc:Http2-Errors-1 | SHOULD | An RST_STREAM is the immediate full closure of the stream and an error propagated to the caller, with the status the Errors table maps its error code to (NO_ERROR, PROTOCOL_ERROR, INTERNAL_ERROR, FLOW_CONTROL_ERROR, SETTINGS_TIMEOUT, FRAME_SIZE_ERROR, COMPRESSION_ERROR, CONNECT_ERROR to INTERNAL; REFUSED_STREAM to UNAVAILABLE; CANCEL to CANCELLED; ENHANCE_YOUR_CALM to RESOURCE_EXHAUSTED; INADEQUATE_SECURITY to PERMISSION_DENIED). | GrpcTransportCases.RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo |
| Grpc:Http2-GoAway-1 | SHOULD | A stream initiated after the GOAWAY's last successfully accepted stream ends as UNAVAILABLE. | GrpcTransportCases.GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable |
| Grpc:Http2-Ping-1 | MUST | A PING from the peer is answered by precisely echoing what was received. | GrpcTransportCases.Ping_FromTheServerBeforeTheReply_IsEchoedPreciselyAndTheCallSucceeds |
| Grpc:Http2-Response-6 | MUST NOT | Decoding an invalid `grpc-message` value does not error or throw the message away: an invalid percent-encoding is shown as it was sent. | GrpcResponseValueCases.GrpcMessage_InvalidPercentEncoding_IsReportedRaw |
| Grpc:Http2-Request-6 | MUST NOT | A metadata value that is a valid HTTP field-value but not a valid ASCII-Value (here a tab) does not fail the call. | GrpcResponseValueCases.Metadata_ValueWithTabOutsideAsciiValue_DoesNotFailTheCall |
| Grpc:Transport-Reset-1 | MUST | A connection reset while the call is read ends the transfer with exit 56 and `Recv failure: Connection was reset`, and while the request is written with exit 55 and `Send failure: Connection was reset`, with no raw .NET text (BL-5028). | Curl.Extended.Protocol.Grpc.UnitTests: GrpcProtocolHandlerTests.ExecuteAsync_ResetWhileReadingWithGrpcProto_FailsWithExit56, ExecuteAsync_ResetWhileReadingReflection_FailsWithExit56, ExecuteAsync_ResetWhileWritingTheRequest_FailsWithExit55 |
