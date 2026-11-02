using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>A unary call over cleartext HTTP/2 with prior knowledge, its request measured against PROTOCOL-HTTP2.md.</summary>
[TestClass]
public sealed class GrpcUnaryCases
{
    internal const int Port = 50051;

    internal const string MethodPath = "/demo.Greeter/SayHello";

    internal const string Proto = """
        syntax = "proto3";
        package demo;
        service Greeter { rpc SayHello (HelloRequest) returns (HelloReply); }
        message HelloRequest { string name = 1; }
        message HelloReply { string message = 1; }
        """;

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Request-1")]
    [SpecRequirement("Grpc:Http2-Request-2")]
    [SpecRequirement("Grpc:Http2-Request-3")]
    [SpecRequirement("Grpc:Http2-Request-4")]
    [SpecRequirement("Grpc:Http2-Request-5")]
    [SpecRequirement("Grpc:Http2-Message-1")]
    [SpecRequirement("Grpc:Http2-Response-1")]
    public Task Unary_OneMessageEachWay_SendsRequestHeadersAndPrintsTheReply() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Request-1", "Grpc:Http2-Request-2", "Grpc:Http2-Request-3", "Grpc:Http2-Request-4", "Grpc:Http2-Request-5", "Grpc:Http2-Message-1", "Grpc:Http2-Response-1"],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("grpc-unary-").FullName;
                try
                {
                    string proto = Path.Combine(directory, "greeter.proto");
                    await File.WriteAllTextAsync(proto, Proto);
                    byte[] reply = [0x0A, 0x04, .. "hi x"u8];
                    GrpcReferenceServer server = new(new GrpcScriptedService(MethodPath, GrpcReferenceReply.Ok(reply)));

                    CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                        CurlRunner,
                        [ReferenceRoute.Tcp(Port, server)],
                        ["-sS", "-m", "10", "--grpc-proto", proto, "-d", """{"name":"x"}""", $"grpc://localhost:{Port}{MethodPath}"]);

                    GrpcReferenceCall call = server.Calls.Single();
                    Assert.AreEqual("POST", call.Header(":method"), "Grpc:Http2-Request-1");
                    Assert.AreEqual("http", call.Header(":scheme"), "Grpc:Http2-Request-2");
                    Assert.AreEqual(MethodPath, call.Header(":path"), "Grpc:Http2-Request-3");
                    Assert.AreEqual("trailers", call.Header("te"), "Grpc:Http2-Request-4");
                    Assert.AreEqual("application/grpc", call.Header("content-type"), "Grpc:Http2-Request-5");
                    CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 3, 0x0A, 0x01, (byte)'x' }, call.DataFrames.SelectMany(static frame => frame).ToArray(), "Grpc:Http2-Message-1");
                    Assert.AreEqual(0, result.ExitCode, "Grpc:Http2-Response-1");
                    Assert.AreEqual("{\n  \"message\": \"hi x\"\n}\n", Encoding.UTF8.GetString(result.StandardOutput), "Grpc:Http2-Response-1");
                    Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError), "Grpc:Http2-Response-1");
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });
}
