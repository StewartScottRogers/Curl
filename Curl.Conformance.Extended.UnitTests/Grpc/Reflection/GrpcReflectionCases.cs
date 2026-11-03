using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Grpc.Reflection;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.Grpc.Reflection.ReflectionDescriptors;

namespace Curl.Conformance.Extended.UnitTests.Grpc.Reflection;

/// <summary>A call made without <c>--grpc-proto</c>, its method described by server reflection, measured against grpc/grpc's server-reflection.md and reflection.proto.</summary>
[TestClass]
public sealed class GrpcReflectionCases
{
    internal const int Port = 50056;

    private const string MethodPath = "/demo.Greeter/SayHello";

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly IReadOnlyList<ProtobufMessageWriter> GreeterMessages =
        [Message("HelloRequest", [Field("name", 1, TypeString)]), Message("HelloReply", [Field("message", 1, TypeString)])];

    private static readonly ProtobufMessageWriter GreeterService = Service("Greeter", "SayHello", ".demo.HelloRequest", ".demo.HelloReply");

    private static readonly GrpcReflectedFile OneFile = new(
        "greeter.proto", File("greeter.proto", "demo", [], GreeterMessages, [], [GreeterService]), ["demo.Greeter"], []);

    private static readonly GrpcReflectedFile[] ImportingFiles =
    [
        new("greeter.proto", File("greeter.proto", "demo", ["messages.proto"], [], [], [GreeterService]), ["demo.Greeter"], ["messages.proto"]),
        new("messages.proto", File("messages.proto", "demo", [], GreeterMessages, [], []), ["demo.HelloRequest", "demo.HelloReply"], []),
    ];

    [TestMethod]
    [SpecRequirement("Grpc:Reflection-Method-1")]
    [SpecRequirement("Grpc:Reflection-Symbol-1")]
    public Task Reflection_ServiceFoundBySymbol_DescribesTheMethodAndMakesTheCall() =>
        Known.RunCaseAsync(
            ["Grpc:Reflection-Method-1", "Grpc:Reflection-Symbol-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await CallAsync(new GrpcReflectionService([OneFile]));

                GrpcReferenceCall reflection = server.Calls[0];
                Assert.AreEqual(GrpcReflectionService.Path, reflection.Path, "Grpc:Reflection-Method-1");
                Assert.AreEqual("application/grpc", reflection.Header("content-type"), "Grpc:Reflection-Method-1");
                Assert.AreEqual(
                    new GrpcReflectionRequest(string.Empty, GrpcReflectionRequest.FileContainingSymbolField, "demo.Greeter"),
                    GrpcReflectionRequest.Parse(reflection.Messages.Single().Payload),
                    "Grpc:Reflection-Symbol-1");
                AssertCallSucceeded(result, server);
            });

    [TestMethod]
    [SpecRequirement("Grpc:Reflection-Dependencies-1")]
    public Task Reflection_FileWithImports_FetchesEachImportByFileName() =>
        Known.RunCaseAsync(
            ["Grpc:Reflection-Dependencies-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await CallAsync(new GrpcReflectionService(ImportingFiles));

                GrpcReflectionRequest[] requests = [.. server.Calls[0].Messages.Select(static message => GrpcReflectionRequest.Parse(message.Payload))];
                CollectionAssert.AreEqual(
                    new[]
                    {
                        new GrpcReflectionRequest(string.Empty, GrpcReflectionRequest.FileContainingSymbolField, "demo.Greeter"),
                        new GrpcReflectionRequest(string.Empty, GrpcReflectionRequest.FileByFilenameField, "messages.proto"),
                    },
                    requests,
                    "Grpc:Reflection-Dependencies-1");
                AssertCallSucceeded(result, server);
            });

    [TestMethod]
    [SpecRequirement("Grpc:Reflection-Dependencies-2")]
    public Task Reflection_FileAndImportsInOneResponse_AsksForNoFileByName() =>
        Known.RunCaseAsync(
            ["Grpc:Reflection-Dependencies-2"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await CallAsync(new GrpcReflectionService(ImportingFiles, sendsDependencies: true));

                Assert.AreEqual(1, server.Calls[0].Messages.Count, "Grpc:Reflection-Dependencies-2");
                AssertCallSucceeded(result, server);
            });

    [TestMethod]
    [SpecRequirement("Grpc:Reflection-Error-1")]
    public Task Reflection_UnknownService_ReportsTheErrorResponseAndMakesNoCall() =>
        Known.RunCaseAsync(
            ["Grpc:Reflection-Error-1"],
            async () =>
            {
                GrpcReflectedFile other = OneFile with { Symbols = ["demo.Other"] };

                (CurlRunResult result, GrpcReferenceServer server) = await CallAsync(new GrpcReflectionService([other]));

                Assert.AreEqual(22, result.ExitCode, "Grpc:Reflection-Error-1");
                Assert.AreEqual(
                    "curl: (22) gRPC server reflection failed (error 5: demo.Greeter not found); give the method's .proto file with --grpc-proto" + Environment.NewLine,
                    Encoding.UTF8.GetString(result.StandardError),
                    "Grpc:Reflection-Error-1");
                Assert.IsEmpty(result.StandardOutput, "Grpc:Reflection-Error-1");
                Assert.AreEqual(1, server.Calls.Count, "Grpc:Reflection-Error-1");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Reflection-Unimplemented-1")]
    public Task Reflection_ServerWithoutReflection_ReportsUnimplementedAndMakesNoCall() =>
        Known.RunCaseAsync(
            ["Grpc:Reflection-Unimplemented-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await CallAsync(new GrpcUnimplementedMethodService(GrpcReflectionService.Path));

                Assert.AreEqual(22, result.ExitCode, "Grpc:Reflection-Unimplemented-1");
                Assert.AreEqual(
                    "curl: (22) The server did not answer gRPC server reflection (grpc-status 12 (UNIMPLEMENTED): unknown service); give the method's .proto file with --grpc-proto" + Environment.NewLine,
                    Encoding.UTF8.GetString(result.StandardError),
                    "Grpc:Reflection-Unimplemented-1");
                Assert.IsEmpty(result.StandardOutput, "Grpc:Reflection-Unimplemented-1");
                Assert.AreEqual(1, server.Calls.Count, "Grpc:Reflection-Unimplemented-1");
            });

    /// <summary>Runs <c>-d {"name":"x"}</c> to <c>demo.Greeter/SayHello</c> without <c>--grpc-proto</c>, against <paramref name="reflection" /> and a SayHello that answers <c>{"message":"hi"}</c>.</summary>
    private static async Task<(CurlRunResult Result, GrpcReferenceServer Server)> CallAsync(IGrpcReferenceService reflection)
    {
        GrpcReferenceServer server = new(reflection, new GrpcScriptedService(MethodPath, GrpcReferenceReply.Ok([0x0A, 0x02, (byte)'h', (byte)'i'])));
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            CurlRunner,
            [ReferenceRoute.Tcp(Port, server)],
            ["-sS", "-m", "10", "-d", """{"name":"x"}""", $"grpc://localhost:{Port}{MethodPath}"]);
        return (result, server);
    }

    private static void AssertCallSucceeded(CurlRunResult result, GrpcReferenceServer server)
    {
        Assert.AreEqual(2, server.Calls.Count);
        Assert.AreEqual(MethodPath, server.Calls[1].Path);
        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x01, (byte)'x' }, server.Calls[1].Messages.Single().Payload);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("{\n  \"message\": \"hi\"\n}\n", Encoding.UTF8.GetString(result.StandardOutput));
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardError));
    }
}
