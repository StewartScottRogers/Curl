using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using Curl.Http2;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// How curl reads the response's Length-Prefixed-Messages (PROTOCOL-HTTP2.md "Length-Prefixed-Message"): one split across
/// DATA frames, one the body ends inside, and one whose Compressed-Flag says it is compressed when curl asked for no encoding.
/// </summary>
[TestClass]
public sealed class GrpcFramingCases
{
    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    // The HelloReply { message: "hi x" } as protobuf, and as its Length-Prefixed-Message (9 bytes).
    private static readonly byte[] ReplyMessage = [0x0A, 0x04, .. "hi x"u8];

    private static readonly byte[] FramedReply = GrpcLengthPrefixedMessage.Frame(ReplyMessage);

    private static readonly HeaderField GrpcStatusOk = new("grpc-status", "0");

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Message-2")]
    public Task Framing_MessageSplitAcrossDataFrames_PrintsTheWholeMessage() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Message-2"],
            async () =>
            {
                GrpcReferenceReply reply = new(GrpcReferenceReply.OkHeaders, [FramedReply[..3], FramedReply[3..]], [GrpcStatusOk]);

                CurlRunResult result = await RunUnaryAsync(reply);

                Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Message-2");
                Assert.AreEqual("{\n  \"message\": \"hi x\"\n}\n", Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Message-2");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Message-2");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Message-3")]
    public Task Framing_BodyEndsInsideAMessage_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Message-3"],
            async () =>
            {
                GrpcReferenceReply reply = new(GrpcReferenceReply.OkHeaders, [FramedReply[..6]], [GrpcStatusOk]);

                CurlRunResult result = await RunUnaryAsync(reply);

                Assert.AreEqual(8, result.ExitCode, "Grpc:Http2-Message-3");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Message-3");
                Assert.AreEqual($"curl: (8) The response body ended inside a gRPC message, 6 bytes into it.{Environment.NewLine}", Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Message-3");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Message-4")]
    public Task Framing_CompressedFlagWithoutAnAcceptedEncoding_FailsWithBadContentEncoding() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Message-4"],
            async () =>
            {
                byte[] compressed = [.. FramedReply];
                compressed[0] = 1;
                GrpcReferenceReply reply = new([.. GrpcReferenceReply.OkHeaders, new("grpc-encoding", "gzip")], [compressed], [GrpcStatusOk]);

                CurlRunResult result = await RunUnaryAsync(reply);

                Assert.AreEqual(61, result.ExitCode, "Grpc:Http2-Message-4");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Message-4");
                Assert.AreEqual($"curl: (61) The server sent a compressed gRPC message, which Curl never asks for{Environment.NewLine}", Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Message-4");
            });

    private static async Task<CurlRunResult> RunUnaryAsync(GrpcReferenceReply reply)
    {
        string directory = Directory.CreateTempSubdirectory("grpc-framing-").FullName;
        try
        {
            string proto = Path.Combine(directory, "greeter.proto");
            await File.WriteAllTextAsync(proto, GrpcUnaryCases.Proto);
            GrpcReferenceServer server = new(new GrpcScriptedService(GrpcUnaryCases.MethodPath, reply));
            return await ExtendedConformanceHarness.RunAsync(
                CurlRunner,
                [ReferenceRoute.Tcp(GrpcUnaryCases.Port, server)],
                ["-sS", "-m", "10", "--grpc-proto", proto, "-d", """{"name":"x"}""", $"grpc://localhost:{GrpcUnaryCases.Port}{GrpcUnaryCases.MethodPath}"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
