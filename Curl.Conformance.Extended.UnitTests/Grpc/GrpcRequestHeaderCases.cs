using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// The Request-Headers PROTOCOL-HTTP2.md "Requests" asks of a client that no other Grpc case carries: no
/// <c>grpc-encoding</c> means every Compressed-Flag is 0, <c>-H</c> custom metadata reaches the server, a <c>-bin</c>
/// key's value goes base64 (un-padded), <c>grpc-timeout</c> follows the pseudo-headers and Call-Definition precedes
/// Custom-Metadata, and a <c>user-agent</c> is sent. A <c>-bin</c> value given to <c>-H</c> is read as base64, the
/// bytes it encodes being the metadata (ADR-1226).
/// </summary>
[TestClass]
public sealed class GrpcRequestHeaderCases
{
    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-7")]
    public Task MessageEncoding_NotSent_EveryCompressedFlagIsZero() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-7"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync([]);

                Assert.IsNull(call.Header("grpc-encoding"), "Grpc:Http2-Request-7: curl asks for no Message-Encoding");
                Assert.IsNotEmpty(call.Messages, "Grpc:Http2-Request-7: the request carries a message");
                Assert.IsTrue(call.Messages.All(static message => !message.IsCompressed), "Grpc:Http2-Request-7");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-8")]
    public Task CustomMetadata_GivenWithH_IsSentAsARequestHeader() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-8"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync(["-H", "x-trace: abc"]);

                Assert.AreEqual("abc", call.Header("x-trace"), "Grpc:Http2-Request-8");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-9")]
    public Task BinaryMetadata_GivenWithH_IsSentBase64Encoded() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-9"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync(["-H", "trace-bin: AQL/"]);

                string? value = call.Header("trace-bin");
                Assert.IsNotNull(value, "Grpc:Http2-Request-9");
                CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0xFF }, DecodeBase64(value), "Grpc:Http2-Request-9");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-10")]
    public Task BinaryMetadata_PaddedInput_IsSentUnpadded() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-10"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync(["-H", "trace-bin: AQI="]);

                Assert.AreEqual("AQI", call.Header("trace-bin"), "Grpc:Http2-Request-10");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-11")]
    public Task HeaderOrder_TimeoutAndMetadata_TimeoutFollowsThePseudoHeadersAndMetadataComesLast() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-11"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync(["-m", "10", "-H", "x-trace: abc"]);

                List<string> names = [.. call.Headers.Select(static header => header.Name)];
                int lastPseudoHeader = names.FindLastIndex(static name => name.StartsWith(':'));
                Assert.AreEqual(lastPseudoHeader + 1, names.IndexOf("grpc-timeout"), $"Grpc:Http2-Request-11: {string.Join(", ", names)}");
                int metadata = names.IndexOf("x-trace");
                Assert.IsGreaterThan(names.IndexOf("content-type"), metadata, $"Grpc:Http2-Request-11: {string.Join(", ", names)}");
                Assert.IsGreaterThan(names.IndexOf("te"), metadata, $"Grpc:Http2-Request-11: {string.Join(", ", names)}");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-12")]
    public Task UserAgent_NotGiven_AUserAgentIsSent() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-12"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync([]);

                string? userAgent = call.Header("user-agent");
                Assert.IsFalse(string.IsNullOrEmpty(userAgent), "Grpc:Http2-Request-12");
            });

    private static byte[] DecodeBase64(string value) =>
        Convert.FromBase64String(value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '='));

    private static async Task<GrpcReferenceCall> RunUnaryAsync(IReadOnlyList<string> extraArguments)
    {
        string directory = Directory.CreateTempSubdirectory("grpc-request-headers-").FullName;
        try
        {
            string proto = Path.Combine(directory, "greeter.proto");
            await File.WriteAllTextAsync(proto, GrpcUnaryCases.Proto);
            GrpcReferenceServer server = new(new GrpcScriptedService(GrpcUnaryCases.MethodPath, GrpcReferenceReply.Ok([0x0A, 0x04, .. "hi x"u8])));
            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                CurlRunner,
                [ReferenceRoute.Tcp(GrpcUnaryCases.Port, server)],
                ["-sS", .. extraArguments, "--grpc-proto", proto, "-d", """{"name":"x"}""", $"grpc://localhost:{GrpcUnaryCases.Port}{GrpcUnaryCases.MethodPath}"]);
            Assert.AreEqual(0, result.ExitCode, "the call succeeds");
            return server.Calls.Single();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
