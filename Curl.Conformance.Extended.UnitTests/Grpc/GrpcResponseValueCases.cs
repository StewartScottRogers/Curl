using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Http2;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// The response values PROTOCOL-HTTP2.md says a client must not fail on: a <c>grpc-message</c> with an invalid
/// percent-encoding and a metadata value that is a valid HTTP field-value but not a valid ASCII-Value.
/// </summary>
[TestClass]
public sealed class GrpcResponseValueCases
{
    private const int Port = 50055;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Response-6")]
    public Task GrpcMessage_InvalidPercentEncoding_IsReportedRaw() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Response-6"],
            async () =>
            {
                (CurlRunResult result, _) = await GrpcResponseCases.CallAsync(GrpcUnaryCases.MethodPath, GrpcReferenceReply.TrailersOnly(5, "bad %zz and %4"), Port);

                Assert.AreEqual(22, result.ExitCode, "Grpc:Http2-Response-6");
                Assert.AreEqual(
                    "curl: (22) grpc-status 5 (NOT_FOUND): bad %zz and %4" + Environment.NewLine,
                    Encoding.UTF8.GetString(result.StandardError),
                    "Grpc:Http2-Response-6");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-6")]
    public Task Metadata_ValueWithTabOutsideAsciiValue_DoesNotFailTheCall() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-6"],
            async () =>
            {
                GrpcReferenceReply ok = GrpcReferenceReply.Ok([0x0A, 0x01, (byte)'a']);
                GrpcReferenceReply reply = ok with { Headers = [.. ok.Headers, new HeaderField("x-note", "a\tb")] };

                (CurlRunResult result, _) = await GrpcResponseCases.CallAsync(GrpcUnaryCases.MethodPath, reply, Port);

                Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Request-6");
                Assert.AreEqual("{\n  \"message\": \"a\"\n}\n", Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Request-6");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Request-6");
            });
}
