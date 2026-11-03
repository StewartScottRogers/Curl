using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Grpc.Reflection;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.Grpc.Reflection.ReflectionDescriptors;

namespace Curl.Conformance.Extended.UnitTests.Grpc.Reflection;

/// <summary>
/// The JSON forms protobuf.dev's "ProtoJSON Format" gives the well-known types other than Timestamp and Duration
/// (<c>Any</c>, <c>Struct</c>/<c>Value</c>/<c>ListValue</c>/<c>NullValue</c>, the wrapper types, <c>FieldMask</c> and
/// <c>Empty</c>) in a call described by server reflection: <c>-d</c> JSON encoded to a request, echoed by the server, and
/// the reply printed as JSON.
/// </summary>
[TestClass]
public sealed class GrpcProtoJsonWellKnownCases
{
    private const int Port = 50058;

    private const string MethodPath = "/types.Known/Echo";

    private const string PointUrl = "type.googleapis.com/types.Point";

    private const string DurationUrl = "type.googleapis.com/google.protobuf.Duration";

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly string[] WrapperTypes =
        ["DoubleValue", "FloatValue", "Int64Value", "UInt64Value", "Int32Value", "UInt32Value", "BoolValue", "StringValue", "BytesValue"];

    private static readonly int[] WrappedTypes = [TypeDouble, TypeFloat, TypeInt64, TypeUInt64, TypeInt32, TypeUInt32, TypeBool, TypeString, TypeBytes];

    private static readonly string[] GoogleFiles =
    [
        "google/protobuf/any.proto", "google/protobuf/struct.proto", "google/protobuf/wrappers.proto", "google/protobuf/field_mask.proto",
        "google/protobuf/empty.proto", "google/protobuf/duration.proto",
    ];

    private static readonly GrpcReflectedFile[] Files =
    [
        new(
            "known.proto",
            File(
                "known.proto",
                "types",
                GoogleFiles,
                [
                    Message(
                        "Known",
                        [
                            Field("payload", 1, TypeMessage, ".google.protobuf.Any"), Field("meta", 2, TypeMessage, ".google.protobuf.Struct"),
                            Field("mask", 3, TypeMessage, ".google.protobuf.FieldMask"), Field("nothing", 4, TypeMessage, ".google.protobuf.Empty"),
                            Field("d", 5, TypeMessage, ".google.protobuf.DoubleValue"), Field("f", 6, TypeMessage, ".google.protobuf.FloatValue"),
                            Field("i64", 7, TypeMessage, ".google.protobuf.Int64Value"), Field("u64", 8, TypeMessage, ".google.protobuf.UInt64Value"),
                            Field("i32", 9, TypeMessage, ".google.protobuf.Int32Value"), Field("u32", 10, TypeMessage, ".google.protobuf.UInt32Value"),
                            Field("b", 11, TypeMessage, ".google.protobuf.BoolValue"), Field("s", 12, TypeMessage, ".google.protobuf.StringValue"),
                            Field("by", 13, TypeMessage, ".google.protobuf.BytesValue"),
                        ]),
                    Message("Point", [Field("x", 1, TypeInt32), Field("y", 2, TypeInt32)]),
                ],
                [],
                [Service("Known", "Echo", ".types.Known", ".types.Known")]),
            ["types.Known", "types.Point"],
            GoogleFiles),
        Google("any.proto", "Any", [Field("type_url", 1, TypeString), Field("value", 2, TypeBytes)]),
        new(
            "google/protobuf/struct.proto",
            File(
                "google/protobuf/struct.proto",
                "google.protobuf",
                [],
                [
                    Message(
                        "Struct",
                        [Field("fields", 1, TypeMessage, ".google.protobuf.Struct.FieldsEntry", isRepeated: true)],
                        [Message("FieldsEntry", [Field("key", 1, TypeString), Field("value", 2, TypeMessage, ".google.protobuf.Value")], isMapEntry: true)]),
                    Message(
                        "Value",
                        [
                            Kind(Field("null_value", 1, TypeEnum, ".google.protobuf.NullValue")), Kind(Field("number_value", 2, TypeDouble)),
                            Kind(Field("string_value", 3, TypeString)), Kind(Field("bool_value", 4, TypeBool)),
                            Kind(Field("struct_value", 5, TypeMessage, ".google.protobuf.Struct")), Kind(Field("list_value", 6, TypeMessage, ".google.protobuf.ListValue")),
                        ]).Message(8, new ProtobufMessageWriter().String(1, "kind")),
                    Message("ListValue", [Field("values", 1, TypeMessage, ".google.protobuf.Value", isRepeated: true)]),
                ],
                [Enum("NullValue", "NULL_VALUE")],
                []),
            ["google.protobuf.Struct", "google.protobuf.Value", "google.protobuf.ListValue", "google.protobuf.NullValue"],
            []),
        new(
            "google/protobuf/wrappers.proto",
            File(
                "google/protobuf/wrappers.proto",
                "google.protobuf",
                [],
                [.. WrapperTypes.Select((name, index) => Message(name, [Field("value", 1, WrappedTypes[index])]))],
                [],
                []),
            [.. WrapperTypes.Select(name => $"google.protobuf.{name}")],
            []),
        Google("field_mask.proto", "FieldMask", [Field("paths", 1, TypeString, isRepeated: true)]),
        Google("empty.proto", "Empty", []),
        Google("duration.proto", "Duration", [Field("seconds", 1, TypeInt64), Field("nanos", 2, TypeInt32)]),
    ];

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Any-1")]
    public Task RoundTrip_AnyOfAMessage_IsTheTypeUrlBesideTheMessagesFields() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Any-1"],
            async () =>
            {
                string json = "{\"payload\":{\"@type\":\"" + PointUrl + "\",\"x\":1,\"y\":2}}";

                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(json);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                CollectionAssert.AreEqual(
                    new ProtobufMessageWriter().Message(1, new ProtobufMessageWriter().String(1, PointUrl).Bytes(2, new ProtobufMessageWriter().Varint(1, 1).Varint(2, 2).ToArray())).ToArray(),
                    server.Calls[^1].Messages.Single().Payload);
                Assert.AreEqual(json, Compact(result.StandardOutput));
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Any-2")]
    public Task RoundTrip_AnyOfAWellKnownType_PutsItsJsonFormInValue() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Any-2"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync("{\"payload\":{\"@type\":\"" + DurationUrl + "\",\"value\":\"1.5s\"}}");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                CollectionAssert.AreEqual(
                    new ProtobufMessageWriter().Message(1, new ProtobufMessageWriter().String(1, DurationUrl).Bytes(2, new ProtobufMessageWriter().Varint(1, 1).Varint(2, 500_000_000).ToArray())).ToArray(),
                    server.Calls[^1].Messages.Single().Payload);
                Assert.AreEqual("{\"payload\":{\"@type\":\"" + DurationUrl + "\",\"value\":\"1.500s\"}}", Compact(result.StandardOutput));
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Struct-1")]
    [SpecRequirement("Grpc:ProtoJson-Struct-2")]
    public Task RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Struct-1", "Grpc:ProtoJson-Struct-2"],
            async () =>
            {
                const string Json = """{"meta":{"a":null,"b":[true,"s",2,{"c":{}}]}}""";

                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(Json);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                ProtobufMessageWriter emptyStruct = new();
                ProtobufMessageWriter list = new ProtobufMessageWriter()
                    .Message(1, new ProtobufMessageWriter().Varint(4, 1))
                    .Message(1, new ProtobufMessageWriter().String(3, "s"))
                    .Message(1, new ProtobufMessageWriter().Fixed64(2, BitConverter.DoubleToUInt64Bits(2)))
                    .Message(1, new ProtobufMessageWriter().Message(5, new ProtobufMessageWriter().Message(1, Entry("c", new ProtobufMessageWriter().Message(5, emptyStruct)))));
                ProtobufMessageWriter meta = new ProtobufMessageWriter()
                    .Message(1, Entry("a", new ProtobufMessageWriter().Varint(1, 0)))
                    .Message(1, Entry("b", new ProtobufMessageWriter().Message(6, list)));
                CollectionAssert.AreEqual(new ProtobufMessageWriter().Message(2, meta).ToArray(), server.Calls[^1].Messages.Single().Payload);
                Assert.AreEqual(Json, Compact(result.StandardOutput));
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Wrappers-1")]
    [SpecRequirement("Grpc:ProtoJson-Wrappers-2")]
    public Task RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Wrappers-1", "Grpc:ProtoJson-Wrappers-2"],
            async () =>
            {
                const string Json = """{"d":1.5,"f":0.25,"i64":"-7","u64":"8","i32":0,"u32":4,"b":true,"s":"x","by":"AAE="}""";

                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(Json);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                byte[] expected = new ProtobufMessageWriter()
                    .Message(5, new ProtobufMessageWriter().Fixed64(1, BitConverter.DoubleToUInt64Bits(1.5)))
                    .Message(6, new ProtobufMessageWriter().Fixed32(1, BitConverter.SingleToUInt32Bits(0.25f)))
                    .Message(7, new ProtobufMessageWriter().Varint(1, -7))
                    .Message(8, new ProtobufMessageWriter().Varint(1, 8))
                    .Message(9, new ProtobufMessageWriter())
                    .Message(10, new ProtobufMessageWriter().Varint(1, 4))
                    .Message(11, new ProtobufMessageWriter().Varint(1, 1))
                    .Message(12, new ProtobufMessageWriter().String(1, "x"))
                    .Message(13, new ProtobufMessageWriter().Bytes(1, [0, 1]))
                    .ToArray();
                CollectionAssert.AreEqual(expected, server.Calls[^1].Messages.Single().Payload, "Grpc:ProtoJson-Wrappers-1");
                Assert.AreEqual(Json, Compact(result.StandardOutput), "Grpc:ProtoJson-Wrappers-2");
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-FieldMask-1")]
    public Task RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-FieldMask-1"],
            async () =>
            {
                const string Json = """{"mask":"fooBar,baz.quxQuux"}""";

                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(Json);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                CollectionAssert.AreEqual(
                    new ProtobufMessageWriter().Message(3, new ProtobufMessageWriter().String(1, "foo_bar").String(1, "baz.qux_quux")).ToArray(),
                    server.Calls[^1].Messages.Single().Payload);
                Assert.AreEqual(Json, Compact(result.StandardOutput));
            });

    [TestMethod]
    [SpecRequirement("Grpc:ProtoJson-Empty-1")]
    public Task RoundTrip_Empty_IsAnEmptyObject() =>
        Known.RunCaseAsync(
            ["Grpc:ProtoJson-Empty-1"],
            async () =>
            {
                const string Json = """{"nothing":{}}""";

                (CurlRunResult result, GrpcReferenceServer server) = await EchoAsync(Json);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                CollectionAssert.AreEqual(new ProtobufMessageWriter().Message(4, new ProtobufMessageWriter()).ToArray(), server.Calls[^1].Messages.Single().Payload);
                Assert.AreEqual(Json, Compact(result.StandardOutput));
            });

    // A google/protobuf/<fileName> declaring the one message <messageName>.
    private static GrpcReflectedFile Google(string fileName, string messageName, IReadOnlyList<ProtobufMessageWriter> fields)
    {
        string name = "google/protobuf/" + fileName;
        return new(name, File(name, "google.protobuf", [], [Message(messageName, fields)], [], []), ["google.protobuf." + messageName], []);
    }

    // A field of Value's "kind" oneof (oneof_index 0), so a zero null_value is still sent.
    private static ProtobufMessageWriter Kind(ProtobufMessageWriter field) => field.Varint(9, 0);

    private static ProtobufMessageWriter Entry(string key, ProtobufMessageWriter value) => new ProtobufMessageWriter().String(1, key).Message(2, value);

    private static async Task<(CurlRunResult Result, GrpcReferenceServer Server)> EchoAsync(string json)
    {
        GrpcReferenceServer server = new(new GrpcReflectionService(Files), new GrpcEchoService(MethodPath));
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            CurlRunner,
            [ReferenceRoute.Tcp(Port, server)],
            ["-sS", "-m", "10", "-d", json, $"grpc://localhost:{Port}{MethodPath}"]);
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
