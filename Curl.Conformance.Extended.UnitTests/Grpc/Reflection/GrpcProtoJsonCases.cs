using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Grpc.Reflection;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.Grpc.Reflection.ReflectionDescriptors;

namespace Curl.Conformance.Extended.UnitTests.Grpc.Reflection;

/// <summary>
/// The proto3 JSON mapping (protobuf.dev "ProtoJSON Format") of a call described by server reflection: <c>-d</c> JSON
/// encoded to a request, echoed by the server, and the reply printed as JSON.
/// </summary>
[TestClass]
public sealed class GrpcProtoJsonCases
{
    private const int Port = 50057;

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly GrpcReflectedFile CommonFile = new(
        "common.proto",
        File(
            "common.proto",
            "types",
            [],
            [Message("Point", [Field("x", 1, TypeInt32), Field("y", 2, TypeInt32)])],
            [Enum("Color", "COLOR_UNSPECIFIED", "RED", "GREEN")],
            []),
        ["types.Point", "types.Color"],
        []);

    private static readonly GrpcReflectedFile TypesFile = new(
        "types.proto",
        File(
            "types.proto",
            "types",
            ["common.proto"],
            [
                Message(
                    "AllTypes",
                    [
                        Field("d", 1, TypeDouble), Field("f", 2, TypeFloat), Field("i32", 3, TypeInt32), Field("i64", 4, TypeInt64),
                        Field("u32", 5, TypeUInt32), Field("u64", 6, TypeUInt64), Field("s32", 7, TypeSInt32), Field("s64", 8, TypeSInt64),
                        Field("fx32", 9, TypeFixed32), Field("fx64", 10, TypeFixed64), Field("sf32", 11, TypeSFixed32), Field("sf64", 12, TypeSFixed64),
                        Field("b", 13, TypeBool), Field("s", 14, TypeString), Field("by", 15, TypeBytes), Field("color", 16, TypeEnum, ".types.Color"),
                        Field("point", 17, TypeMessage, ".types.Point"), Field("numbers", 18, TypeInt32, isRepeated: true),
                        Field("counts", 19, TypeMessage, ".types.AllTypes.CountsEntry", isRepeated: true), Field("snake_name", 20, TypeString),
                    ],
                    [Message("CountsEntry", [Field("key", 1, TypeString), Field("value", 2, TypeInt32)], isMapEntry: true)]),
            ],
            [],
            [Service("Echo", "Echo", ".types.AllTypes", ".types.AllTypes")]),
        ["types.Echo"],
        ["common.proto"]);

    private static readonly GrpcReflectedFile[] WellKnownFiles =
    [
        new(
            "times.proto",
            File(
                "times.proto",
                "types",
                ["google/protobuf/timestamp.proto", "google/protobuf/duration.proto"],
                [Message("Times", [Field("at", 1, TypeMessage, ".google.protobuf.Timestamp"), Field("took", 2, TypeMessage, ".google.protobuf.Duration")])],
                [],
                [Service("Clock", "Echo", ".types.Times", ".types.Times")]),
            ["types.Clock"],
            ["google/protobuf/timestamp.proto", "google/protobuf/duration.proto"]),
        new(
            "google/protobuf/timestamp.proto",
            File("google/protobuf/timestamp.proto", "google.protobuf", [], [Message("Timestamp", [Field("seconds", 1, TypeInt64), Field("nanos", 2, TypeInt32)])], [], []),
            ["google.protobuf.Timestamp"],
            []),
        new(
            "google/protobuf/duration.proto",
            File("google/protobuf/duration.proto", "google.protobuf", [], [Message("Duration", [Field("seconds", 1, TypeInt64), Field("nanos", 2, TypeInt32)])], [], []),
            ["google.protobuf.Duration"],
            []),
    ];

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Names-1")]
    [SpecRequirement("Grpc:ProtoJson-Int64-1")]
    [SpecRequirement("Grpc:ProtoJson-Int32-1")]
    [SpecRequirement("Grpc:ProtoJson-Float-1")]
    [SpecRequirement("Grpc:ProtoJson-Bool-1")]
    [SpecRequirement("Grpc:ProtoJson-String-1")]
    [SpecRequirement("Grpc:ProtoJson-Bytes-1")]
    [SpecRequirement("Grpc:ProtoJson-Enum-1")]
    [SpecRequirement("Grpc:ProtoJson-Message-1")]
    [SpecRequirement("Grpc:ProtoJson-Repeated-1")]
    [SpecRequirement("Grpc:ProtoJson-Map-1")]
    public Task RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Names-1", "Grpc:ProtoJson-Int64-1", "Grpc:ProtoJson-Int32-1", "Grpc:ProtoJson-Float-1", "Grpc:ProtoJson-Bool-1", "Grpc:ProtoJson-String-1",
             "Grpc:ProtoJson-Bytes-1", "Grpc:ProtoJson-Enum-1", "Grpc:ProtoJson-Message-1", "Grpc:ProtoJson-Repeated-1", "Grpc:ProtoJson-Map-1"],
            async () =>
            {
                const string Json = """
                    {"d":1.5,"f":0.25,"i32":-3,"i64":"9007199254740993","u32":4,"u64":"18446744073709551615","s32":-5,"s64":"-6","fx32":7,"fx64":"8","sf32":-9,"sf64":"-10","b":true,"s":"hi","by":"AAEC","color":"RED","point":{"x":1,"y":2},"numbers":[1,2,3],"counts":{"a":1},"snakeName":"z"}
                    """;

                (CurlRunResult result, _) = await EchoAsync([TypesFile, CommonFile], "/types.Echo/Echo", Json);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual(Json, Compact(result.StandardOutput));
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Names-2")]
    [SpecRequirement("Grpc:ProtoJson-Int64-2")]
    [SpecRequirement("Grpc:ProtoJson-Int32-2")]
    [SpecRequirement("Grpc:ProtoJson-Float-2")]
    [SpecRequirement("Grpc:ProtoJson-Float-3")]
    [SpecRequirement("Grpc:ProtoJson-Bytes-2")]
    [SpecRequirement("Grpc:ProtoJson-Enum-2")]
    public Task Parse_AlternativeForms_EncodesTheSameValues() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Names-2", "Grpc:ProtoJson-Int64-2", "Grpc:ProtoJson-Int32-2", "Grpc:ProtoJson-Float-2", "Grpc:ProtoJson-Float-3", "Grpc:ProtoJson-Bytes-2", "Grpc:ProtoJson-Enum-2"],
            async () =>
            {
                (CurlRunResult result, _) = await EchoAsync(
                    [TypesFile, CommonFile],
                    "/types.Echo/Echo",
                    """{"d":"NaN","f":2.5e1,"i32":"7","i64":5,"u64":12,"by":"_w","color":2,"snake_name":"a"}""");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual("""{"d":"NaN","f":25,"i32":7,"i64":"5","u64":"12","by":"/w==","color":"GREEN","snakeName":"a"}""", Compact(result.StandardOutput));
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Default-1")]
    [SpecRequirement("Grpc:ProtoJson-Null-1")]
    public Task RoundTrip_DefaultsAndNulls_SendsAndPrintsNothing() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Default-1", "Grpc:ProtoJson-Null-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(
                    [TypesFile, CommonFile],
                    "/types.Echo/Echo",
                    """{"d":0,"i32":0,"i64":"0","s":"","b":false,"by":"","color":"COLOR_UNSPECIFIED","numbers":[],"counts":{},"point":null,"s32":null}""");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.IsEmpty(server.Calls[1].Messages.Single().Payload, "Grpc:ProtoJson-Null-1");
                Assert.AreEqual("{}", Compact(result.StandardOutput), "Grpc:ProtoJson-Default-1");
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Unknown-1")]
    public Task Parse_UnknownField_IsRejectedBeforeTheCall() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Unknown-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync([TypesFile, CommonFile], "/types.Echo/Echo", """{"nope":1}""");

                Assert.AreEqual(2, result.ExitCode);
                StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (2) -d: the data is not a types.AllTypes message as JSON");
                Assert.AreEqual(1, server.Calls.Count);
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Timestamp-1")]
    [SpecRequirement("Grpc:ProtoJson-Duration-1")]
    public Task RoundTrip_TimestampAndDuration_UseTheirStringForms() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Timestamp-1", "Grpc:ProtoJson-Duration-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(WellKnownFiles, "/types.Clock/Echo", """{"at":"1970-01-01T00:00:01Z","took":"1.5s"}""");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                CollectionAssert.AreEqual(
                    new ProtobufMessageWriter().Message(1, new ProtobufMessageWriter().Varint(1, 1)).Message(2, new ProtobufMessageWriter().Varint(1, 1).Varint(2, 500_000_000)).ToArray(),
                    server.Calls[^1].Messages.Single().Payload);
                Assert.AreEqual("""{"at":"1970-01-01T00:00:01Z","took":"1.500s"}""", Compact(result.StandardOutput));
            });

    private static async Task<(CurlRunResult Result, GrpcReferenceServer Server)> EchoAsync(IReadOnlyList<GrpcReflectedFile> files, string methodPath, string json)
    {
        GrpcReferenceServer server = new(new GrpcReflectionService(files), new GrpcEchoService(methodPath));
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            CurlRunner,
            [ReferenceRoute.Tcp(Port, server)],
            ["-sS", "-m", "10", "-d", json, $"grpc://localhost:{Port}{methodPath}"]);
        return (result, server);
    }

    // The output is indented; none of these values holds a space or a line end, so dropping them leaves the JSON itself.
    private static string Compact(byte[] output)
    {
        string text = Encoding.UTF8.GetString(output);
        Assert.EndsWith("\n", text);
        return text.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}
