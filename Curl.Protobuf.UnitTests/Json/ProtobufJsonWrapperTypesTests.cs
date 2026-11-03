using System.Text;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Pins the JSON forms of the wrapper types (protobuf.dev/programming-guides/json): the wrapped
/// value itself, both ways, with a present zero kept.
/// </summary>
[TestClass]
public sealed class ProtobufJsonWrapperTypesTests
{
    private const string Proto = """
        syntax = "proto3";
        package google.protobuf;
        message DoubleValue { double value = 1; }
        message Int64Value { int64 value = 1; }
        message Int32Value { int32 value = 1; }
        message BoolValue { bool value = 1; }
        message StringValue { string value = 1; }
        message BytesValue { bytes value = 1; }
        message Wrapped { DoubleValue d = 1; Int64Value i64 = 2; Int32Value i32 = 3; BoolValue b = 4; StringValue s = 5; BytesValue by = 6; repeated Int32Value many = 7; }
        """;

    private static readonly DynamicTypeCatalogue Catalogue = CreateCatalogue();

    private static DynamicMessageType Wrapped => Catalogue.GetMessageType("google.protobuf.Wrapped");

    [TestMethod]
    public void Wrappers_RoundTrip_AsTheirWrappedValues()
    {
        const string Json = """{"d":1.5,"i64":"-7","i32":4,"b":true,"s":"x","by":"AAE=","many":[1,0]}""";

        var message = Parse(Json);

        Assert.AreEqual(4, ((DynamicMessage)message.GetValue("i32")).GetValue("value"));
        Assert.AreEqual(Json, ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void Wrapper_HoldingZero_IsWrittenAsZeroAndEncodedAsAnEmptyMessage()
    {
        var message = Parse("""{"i32":0,"s":""}""");

        Assert.AreEqual("""{"i32":0,"s":""}""", ProtobufJsonFormatter.Format(message, indented: false));
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonConverter.ConvertJsonToBinary(Wrapped, Encoding.UTF8.GetBytes("""{"i32":0}"""), out var binary));
        Assert.AreEqual("1A00", Convert.ToHexString(binary!));
    }

    [TestMethod]
    public void Wrapper_SetWithoutAValue_IsWrittenAsTheDefault()
    {
        var message = new DynamicMessage(Wrapped);
        message.SetValue("b", new DynamicMessage(Wrapped.FindField("b")!.MessageType!));

        Assert.AreEqual("""{"b":false}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    [DataRow("""{"i32":"x"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"i32":{"value":1}}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"b":1}""", ProtobufJsonError.WrongType)]
    public void Wrapper_OfTheWrongValue_IsRejected(string json, ProtobufJsonError expected) =>
        Assert.AreEqual(expected, ProtobufJsonParser.Parse(Wrapped, Encoding.UTF8.GetBytes(json), out _));

    private static DynamicMessage Parse(string json)
    {
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonParser.Parse(Wrapped, Encoding.UTF8.GetBytes(json), out var message));
        return message!;
    }

    private static DynamicTypeCatalogue CreateCatalogue()
    {
        Assert.IsTrue(ProtoParser.TryParse(Proto, out var file, out var error), error?.ToString());
        return new DynamicTypeCatalogue([file]);
    }
}
