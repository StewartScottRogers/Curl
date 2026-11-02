using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using Curl.Http2;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// The responses of PROTOCOL-HTTP2.md "Responses" a client must read: a server-streaming reply, a Trailers-Only reply, a
/// percent-encoded <c>grpc-message</c> and a non-200 HTTP status, each measured against the specification.
/// </summary>
[TestClass]
public sealed class GrpcResponseCases
{
    private const int Port = 50052;

    private const string StreamingMethodPath = "/demo.Greeter/SayHellos";

    private const string StreamingProto = """
        syntax = "proto3";
        package demo;
        service Greeter {
          rpc SayHello (HelloRequest) returns (HelloReply);
          rpc SayHellos (HelloRequest) returns (stream HelloReply);
        }
        message HelloRequest { string name = 1; }
        message HelloReply { string message = 1; }
        """;

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly byte[] RequestFrame = [0, 0, 0, 0, 3, 0x0A, 0x01, (byte)'x'];

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Response-2")]
    public Task ServerStreaming_ThreeMessagesThenOk_PrintsEachMessageInOrder() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Response-2"],
            async () =>
            {
                GrpcReferenceReply reply = GrpcReferenceReply.Ok(HelloReply("a"), HelloReply("bb"), HelloReply("ccc"));
                (CurlRunResult result, GrpcReferenceCall call) = await CallAsync(StreamingMethodPath, reply);

                CollectionAssert.AreEqual(RequestFrame, call.DataFrames.SelectMany(static frame => frame).ToArray(), "Grpc:Http2-Response-2");
                Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Response-2");
                Assert.AreEqual(
                    "{\n  \"message\": \"a\"\n}\n{\n  \"message\": \"bb\"\n}\n{\n  \"message\": \"ccc\"\n}\n",
                    Encoding.UTF8.GetString(result.StandardOutput),
                    "Grpc:Http2-Response-2");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Response-2");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Response-3")]
    public Task TrailersOnly_OkInTheOnlyHeaders_EndsTheCallWithNoOutput() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Response-3"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceCall call) = await CallAsync(GrpcUnaryCases.MethodPath, GrpcReferenceReply.TrailersOnly(0));

                CollectionAssert.AreEqual(RequestFrame, call.DataFrames.SelectMany(static frame => frame).ToArray(), "Grpc:Http2-Response-3");
                Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Response-3");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Response-3");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Response-3");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Response-4")]
    public Task GrpcMessage_PercentEncoded_IsReportedDecoded() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Response-4"],
            async () =>
            {
                GrpcReferenceReply reply = GrpcReferenceReply.TrailersOnly(5, "no%20user%20%22x%22%3A%2050%25%20gone");
                (CurlRunResult result, _) = await CallAsync(GrpcUnaryCases.MethodPath, reply);

                Assert.AreEqual(22, result.ExitCode, "Grpc:Http2-Response-4");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Response-4");
                Assert.AreEqual(
                    "curl: (22) grpc-status 5 (NOT_FOUND): no user \"x\": 50% gone" + Environment.NewLine,
                    Encoding.UTF8.GetString(result.StandardError),
                    "Grpc:Http2-Response-4");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Response-5")]
    [DataRow("400", 13, "INTERNAL")]
    [DataRow("401", 16, "UNAUTHENTICATED")]
    [DataRow("403", 7, "PERMISSION_DENIED")]
    [DataRow("404", 12, "UNIMPLEMENTED")]
    [DataRow("429", 14, "UNAVAILABLE")]
    [DataRow("502", 14, "UNAVAILABLE")]
    [DataRow("503", 14, "UNAVAILABLE")]
    [DataRow("504", 14, "UNAVAILABLE")]
    [DataRow("500", 2, "UNKNOWN")]
    public Task HttpStatus_NotOkWithNoGrpcStatus_EndsTheCallWithItsMappedCode(string httpStatus, int code, string name) =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Response-5"],
            async () =>
            {
                GrpcReferenceReply reply = new([new HeaderField(":status", httpStatus), new HeaderField("content-type", "text/plain")], [], [], IsTrailersOnly: true);
                (CurlRunResult result, _) = await CallAsync(GrpcUnaryCases.MethodPath, reply);

                Assert.AreEqual(22, result.ExitCode, "Grpc:Http2-Response-5");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Response-5");
                Assert.AreEqual(
                    $"curl: (22) grpc-status {code} ({name}): HTTP status {httpStatus}{Environment.NewLine}",
                    Encoding.UTF8.GetString(result.StandardError),
                    "Grpc:Http2-Response-5");
            });

    /// <summary>Runs curl's call of <paramref name="methodPath" /> with <c>{"name":"x"}</c> against a server scripted to answer <paramref name="reply" />.</summary>
    internal static async Task<(CurlRunResult Result, GrpcReferenceCall Call)> CallAsync(string methodPath, GrpcReferenceReply reply, int port = Port)
    {
        string directory = Directory.CreateTempSubdirectory("grpc-response-").FullName;
        try
        {
            string proto = Path.Combine(directory, "greeter.proto");
            await File.WriteAllTextAsync(proto, StreamingProto);
            GrpcReferenceServer server = new(new GrpcScriptedService(methodPath, reply));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                CurlRunner,
                [ReferenceRoute.Tcp(port, server)],
                ["-sS", "-m", "10", "--grpc-proto", proto, "-d", """{"name":"x"}""", $"grpc://localhost:{port}{methodPath}"]);
            return (result, server.Calls.Single());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] HelloReply(string message) => [0x0A, (byte)message.Length, .. Encoding.ASCII.GetBytes(message)];
}
