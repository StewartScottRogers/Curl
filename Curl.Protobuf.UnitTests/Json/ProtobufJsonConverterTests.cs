using System.Text;
using Curl.Protobuf.DynamicMessages;
using static Curl.Protobuf.Json.JsonTestTypes;

namespace Curl.Protobuf.Json;

/// <summary>Pins <see cref="ProtobufJsonConverter" />, JSON to Protocol Buffers binary and back.</summary>
[TestClass]
public sealed class ProtobufJsonConverterTests
{
    [TestMethod]
    public void ConvertJsonToBinary_Int32150_IsTheSpecificationsVector()
    {
        var error = ProtobufJsonConverter.ConvertJsonToBinary(Type("Scalars"), Encoding.UTF8.GetBytes("""{"fInt32":150}"""), out var binary);

        Assert.AreEqual(ProtobufJsonError.None, error);
        Assert.AreEqual("189601", Convert.ToHexString(binary!));
    }

    [TestMethod]
    public void ConvertJsonToBinary_UnknownMember_HasNoBinary()
    {
        var error = ProtobufJsonConverter.ConvertJsonToBinary(Type("Scalars"), Encoding.UTF8.GetBytes("""{"nope":1}"""), out var binary);

        Assert.AreEqual(ProtobufJsonError.UnknownField, error);
        Assert.IsNull(binary);
    }

    [TestMethod]
    public void ConvertBinaryToJson_Int32150_IsItsJson()
    {
        var error = ProtobufJsonConverter.ConvertBinaryToJson(Type("Scalars"), Convert.FromHexString("189601"), indented: false, out var json);

        Assert.AreEqual(ProtobufJsonError.None, error);
        Assert.AreEqual("""{"fInt32":150}""", json);
    }

    [TestMethod]
    public void ConvertBinaryToJson_TruncatedVarint_IsMalformedBinary()
    {
        var error = ProtobufJsonConverter.ConvertBinaryToJson(Type("Scalars"), Convert.FromHexString("1896"), indented: false, out var json);

        Assert.AreEqual(ProtobufJsonError.MalformedBinary, error);
        Assert.IsNull(json);
    }

    [TestMethod]
    public void ConvertBinaryToJson_MessagesNestedPastOneHundred_IsNestingTooDeep()
    {
        var outer = new DynamicMessage(Type("Node"));
        outer.SetValue("next", Parse("Node", ProtobufJsonParserTests.NestedNodes(100)));

        var error = ProtobufJsonConverter.ConvertBinaryToJson(Type("Node"), DynamicMessageEncoder.Encode(outer), indented: false, out var json);

        Assert.AreEqual(ProtobufJsonError.NestingTooDeep, error);
        Assert.IsNull(json);
    }

    [TestMethod]
    public void Convert_JsonToBinaryAndBack_KeepsEveryValue()
    {
        const string Json = """{"numbers":["1","-2"],"items":[{"fString":"x","fBytes":"AQID"}],"colors":["GREEN"]}""";
        ProtobufJsonConverter.ConvertJsonToBinary(Type("Lists"), Encoding.UTF8.GetBytes(Json), out var binary);

        var error = ProtobufJsonConverter.ConvertBinaryToJson(Type("Lists"), binary, indented: false, out var json);

        Assert.AreEqual(ProtobufJsonError.None, error);
        Assert.AreEqual(Json, json);
    }
}
