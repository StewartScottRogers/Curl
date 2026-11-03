using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Http2;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// The HTTP/2 transport mapping of PROTOCOL-HTTP2.md a client must honour when the server sends RST_STREAM, GOAWAY or a
/// PING of its own, each measured against the specification.
/// </summary>
[TestClass]
public sealed class GrpcTransportCases
{
    private const int Port = 50054;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Errors-1")]
    [DataRow(Http2ErrorCode.NoError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.ProtocolError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.InternalError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.FlowControlError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.SettingsTimeout, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.FrameSizeError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.RefusedStream, 14, "UNAVAILABLE")]
    [DataRow(Http2ErrorCode.Cancel, 1, "CANCELLED")]
    [DataRow(Http2ErrorCode.CompressionError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.ConnectError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.EnhanceYourCalm, 8, "RESOURCE_EXHAUSTED")]
    [DataRow(Http2ErrorCode.InadequateSecurity, 7, "PERMISSION_DENIED")]
    public Task RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo(Http2ErrorCode errorCode, int code, string name) =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Errors-1"],
            async () =>
            {
                (CurlRunResult result, _) = await GrpcResponseCases.CallAsync(GrpcUnaryCases.MethodPath, GrpcReferenceReply.Reset(errorCode), Port);

                Assert.AreEqual(22, result.ExitCode, "Grpc:Http2-Errors-1");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Errors-1");
                Assert.StartsWith($"curl: (22) grpc-status {code} ({name}): ", Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Errors-1");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-GoAway-1")]
    public Task GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-GoAway-1"],
            async () =>
            {
                (CurlRunResult result, _) = await GrpcResponseCases.CallAsync(GrpcUnaryCases.MethodPath, GrpcReferenceReply.GoAway(0), Port);

                Assert.AreEqual(22, result.ExitCode, "Grpc:Http2-GoAway-1");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-GoAway-1");
                Assert.StartsWith("curl: (22) grpc-status 14 (UNAVAILABLE): ", Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-GoAway-1");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Ping-1")]
    public Task Ping_FromTheServerBeforeTheReply_IsEchoedPreciselyAndTheCallSucceeds() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Ping-1"],
            async () =>
            {
                GrpcReferencePing ping = new(0xFEDCBA9876543210);
                GrpcReferenceReply reply = GrpcReferenceReply.Ok([0x0A, 0x01, (byte)'a']) with { PingBeforeReply = ping };

                (CurlRunResult result, _) = await GrpcResponseCases.CallAsync(GrpcUnaryCases.MethodPath, reply, Port);

                Assert.IsTrue(ping.IsEchoed, "Grpc:Http2-Ping-1");
                Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Ping-1");
                Assert.AreEqual("{\n  \"message\": \"a\"\n}\n", Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Ping-1");
            });
}
