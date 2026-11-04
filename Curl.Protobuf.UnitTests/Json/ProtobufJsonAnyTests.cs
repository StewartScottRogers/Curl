using System.Text;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Pins the JSON form of <c>google.protobuf.Any</c> (protobuf.dev/programming-guides/json): <c>"@type"</c>
/// beside the embedded message's fields, or beside <c>"value"</c> for a well-known type with its own form.
/// </summary>
[TestClass]
public sealed class ProtobufJsonAnyTests
{
    private const string Proto = """
        syntax = "proto3";
        package google.protobuf;
        message Any { string type_url = 1; bytes value = 2; }
        message Duration { int64 seconds = 1; int32 nanos = 2; }
        message Int32Value { int32 value = 1; }
        message Empty { }
        message Point { int32 x = 1; int32 y = 2; }
        message Holder { Any a = 1; }
        """;

    private const string PointUrl = "type.googleapis.com/google.protobuf.Point";

    private const string AnyUrl = "type.googleapis.com/google.protobuf.Any";

    private static readonly DynamicTypeCatalogue Catalogue = CreateCatalogue();

    private static DynamicMessageType Holder => Catalogue.GetMessageType("google.protobuf.Holder");

    private static DynamicMessageType Any => Catalogue.GetMessageType("google.protobuf.Any");

    [TestMethod]
    public void AnyOfAMessage_IsTheTypeUrlBesideTheMessagesFields()
    {
        const string Json = """{"a":{"@type":"type.googleapis.com/google.protobuf.Point","x":1,"y":2}}""";

        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonConverter.ConvertJsonToBinary(Holder, Encoding.UTF8.GetBytes(Json), out var binary));

        // a (1) { type_url (1) = PointUrl, value (2) = { x (1) = 1, y (2) = 2 } }
        var url = Convert.ToHexString(Encoding.UTF8.GetBytes(PointUrl));
        Assert.AreEqual("0A" + "31" + "0A" + "29" + url + "1204" + "08011002", Convert.ToHexString(binary!));
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonConverter.ConvertBinaryToJson(Holder, binary, indented: false, out var json));
        Assert.AreEqual(Json, json);
    }

    [TestMethod]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Duration","value":"1.500s"}}""")]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Int32Value","value":0}}""")]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Any","value":{"@type":"type.googleapis.com/google.protobuf.Point","x":3}}}""")]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Empty"}}""")]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Point"}}""")]
    [DataRow("""{"a":{}}""")]
    public void Any_RoundTrips(string json) => Assert.AreEqual(json, ProtobufJsonFormatter.Format(Parse(json), indented: false));

    [TestMethod]
    public void AnyOfAWellKnownType_PutsItsJsonFormInValue()
    {
        var message = Parse("""{"a":{"value":"1.5s","@type":"type.googleapis.com/google.protobuf.Duration"}}""");

        var any = (DynamicMessage)message.GetValue("a");
        Assert.AreEqual("08011080CAB5EE01", Convert.ToHexString((byte[])any.GetValue("value")));
        Assert.AreEqual("""{"a":{"@type":"type.googleapis.com/google.protobuf.Duration","value":"1.500s"}}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    [DataRow("""{"a":5}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"a":{"x":1}}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"a":{"@type":5}}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"a":{"@type":"google.protobuf.Point"}}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Nothing"}}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Point","z":1}}""", ProtobufJsonError.UnknownField)]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Point","x":"one"}}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Duration","x":1}}""", ProtobufJsonError.UnknownField)]
    [DataRow("""{"a":{"@type":"type.googleapis.com/google.protobuf.Duration","value":5}}""", ProtobufJsonError.WrongType)]
    public void Any_Malformed_IsRejected(string json, ProtobufJsonError expected) =>
        Assert.AreEqual(expected, ProtobufJsonParser.Parse(Holder, Encoding.UTF8.GetBytes(json), out _));

    [TestMethod]
    public void AnyOfAny_NestedTooDeep_IsRejected()
    {
        var open = "{\"@type\":\"" + AnyUrl + "\",\"value\":";
        var json = "{\"a\":" + string.Concat(Enumerable.Repeat(open, 200)) + "{}" + new string('}', 201);

        Assert.AreEqual(ProtobufJsonError.NestingTooDeep, ProtobufJsonParser.Parse(Holder, Encoding.UTF8.GetBytes(json), out _));
    }

    [TestMethod]
    [DataRow("type.googleapis.com/google.protobuf.Nothing", "")]
    [DataRow("google.protobuf.Point", "")]
    [DataRow("", "0801")]
    [DataRow(PointUrl, "FF")]
    public void Any_ThatDoesNotResolve_HasNoJsonForm(string url, string valueHex)
    {
        var message = HolderOf(AnyOf(url, Convert.FromHexString(valueHex)));

        Assert.ThrowsExactly<FormatException>(() => ProtobufJsonFormatter.Format(message, indented: false));
        Assert.AreEqual(
            ProtobufJsonError.InvalidValue,
            ProtobufJsonConverter.ConvertBinaryToJson(Holder, DynamicMessageEncoder.Encode(message), indented: false, out _));
    }

    [TestMethod]
    public void AnyOfAny_NestedTooDeep_HasNoJsonForm()
    {
        var any = AnyOf(PointUrl, []);
        for (var level = 0; level < 120; level++)
        {
            any = AnyOf(AnyUrl, DynamicMessageEncoder.Encode(any));
        }

        Assert.ThrowsExactly<FormatException>(() => ProtobufJsonFormatter.Format(HolderOf(any), indented: false));
    }

    private static DynamicMessage AnyOf(string url, byte[] value)
    {
        var any = new DynamicMessage(Any);
        any.SetValue("type_url", url);
        any.SetValue("value", value);
        return any;
    }

    private static DynamicMessage HolderOf(DynamicMessage any)
    {
        var holder = new DynamicMessage(Holder);
        holder.SetValue("a", any);
        return holder;
    }

    private static DynamicMessage Parse(string json)
    {
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonParser.Parse(Holder, Encoding.UTF8.GetBytes(json), out var message));
        return message!;
    }

    private static DynamicTypeCatalogue CreateCatalogue()
    {
        Assert.IsTrue(ProtoParser.TryParse(Proto, out var file, out var error), error?.ToString());
        return new DynamicTypeCatalogue([file]);
    }
}
