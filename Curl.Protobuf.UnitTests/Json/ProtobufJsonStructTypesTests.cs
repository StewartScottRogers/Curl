using System.Text;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Pins the JSON forms of <c>google.protobuf.Struct</c>, <c>Value</c>, <c>ListValue</c> and
/// <c>NullValue</c> (protobuf.dev/programming-guides/json): any JSON object, value, array and null.
/// </summary>
[TestClass]
public sealed class ProtobufJsonStructTypesTests
{
    private const string Proto = """
        syntax = "proto3";
        package google.protobuf;
        message Struct { map<string, Value> fields = 1; }
        message Value {
          oneof kind {
            NullValue null_value = 1; double number_value = 2; string string_value = 3;
            bool bool_value = 4; Struct struct_value = 5; ListValue list_value = 6;
          }
        }
        message ListValue { repeated Value values = 1; }
        enum NullValue { NULL_VALUE = 0; }
        message Holder { Struct meta = 1; Value v = 2; ListValue list = 3; optional NullValue n = 4; repeated NullValue ns = 5; map<string, Value> byName = 6; string name = 7; }
        """;

    private static readonly DynamicTypeCatalogue Catalogue = CreateCatalogue();

    private static DynamicMessageType Holder => Catalogue.GetMessageType("google.protobuf.Holder");

    [TestMethod]
    [DataRow("""{"meta":{"a":null,"b":[true,"s",2,{"c":{}}],"d":false,"e":[]}}""")]
    [DataRow("""{"meta":{}}""")]
    [DataRow("""{"v":null}""")]
    [DataRow("""{"v":1.5}""")]
    [DataRow("""{"v":{"x":[[]]}}""")]
    [DataRow("""{"list":[1,"two",null]}""")]
    [DataRow("""{"byName":{"k":"v"}}""")]
    public void StructTypes_RoundTrip_AsAnyJson(string json) =>
        Assert.AreEqual(json, ProtobufJsonFormatter.Format(Parse(json), indented: false));

    [TestMethod]
    public void Struct_Null_IsANullValueEncodedAsKindZero()
    {
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonConverter.TryConvertJsonToBinary(Holder, Encoding.UTF8.GetBytes("""{"meta":{"a":null}}"""), out var binary));

        // meta (1) { fields (1) { key "a", value (2) { null_value (1) = 0 } } }
        Assert.AreEqual("0A090A070A016112020800", Convert.ToHexString(binary!));
    }

    [TestMethod]
    public void Null_InAFieldOfNoValueType_LeavesItUnset() =>
        Assert.AreEqual("{}", ProtobufJsonFormatter.Format(Parse("""{"name":null,"ns":null,"meta":null,"list":null}"""), indented: false));

    [TestMethod]
    public void NullValue_IsReadFromNullOrItsNameAndWrittenAsNull()
    {
        var message = Parse("""{"n":null,"ns":[null,"NULL_VALUE",0]}""");

        Assert.AreEqual(0, message.GetValue("n"));
        Assert.AreEqual("""{"n":null,"ns":[null,null,null]}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void StructTypes_SetEmpty_AreAnEmptyObjectAndArray()
    {
        var message = new DynamicMessage(Holder);
        message.SetValue("meta", new DynamicMessage(Holder.FindField("meta")!.MessageType!));
        message.SetValue("list", new DynamicMessage(Holder.FindField("list")!.MessageType!));

        Assert.AreEqual("""{"meta":{},"list":[]}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void Value_WithNoKindSet_CannotBeWritten()
    {
        var message = new DynamicMessage(Holder);
        message.SetValue("v", new DynamicMessage(Holder.FindField("v")!.MessageType!));

        Assert.ThrowsExactly<FormatException>(() => ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    [DataRow("""{"meta":5}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"list":{}}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"v":1e400}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"meta":{"a":1e400,"b":1}}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"list":[1e400,1]}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"n":"SOMETHING"}""", ProtobufJsonError.InvalidValue)]
    public void StructTypes_OfTheWrongKind_AreRejected(string json, ProtobufJsonError expected) =>
        Assert.AreEqual(expected, ProtobufJsonParser.Parse(Holder, Encoding.UTF8.GetBytes(json), out _));

    [TestMethod]
    [DataRow("[", "]")]
    [DataRow("{\"a\":", "}")]
    public void StructTypes_NestedTooDeep_AreRejected(string open, string close)
    {
        var json = "{\"v\":" + string.Concat(Enumerable.Repeat(open, 200)) + "1" + string.Concat(Enumerable.Repeat(close, 200)) + "}";

        Assert.AreEqual(ProtobufJsonError.NestingTooDeep, ProtobufJsonParser.Parse(Holder, Encoding.UTF8.GetBytes(json), out _));
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
