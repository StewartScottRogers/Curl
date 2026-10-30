using System.Text;
using Curl.Protobuf.DynamicMessages;
using static Curl.Protobuf.Json.JsonTestTypes;

namespace Curl.Protobuf.Json;

/// <summary>Pins <see cref="ProtobufJsonParser" /> against the rules of the proto3 JSON mapping.</summary>
[TestClass]
public sealed class ProtobufJsonParserTests
{
    [TestMethod]
    [DataRow("""{"fInt32":3}""")]
    [DataRow("""{"f_int32":3}""")]
    public void TryParse_LowerCamelCaseOrOriginalName_SetsTheField(string json)
    {
        var message = Parse("Scalars", json);

        Assert.AreEqual(3, message.GetValue("f_int32"));
    }

    [TestMethod]
    public void TryParse_JsonNameOption_NamesTheField()
    {
        var message = Parse("Scalars", """{"custom":17}""");

        Assert.AreEqual(17, message.GetValue("renamed"));
    }

    [TestMethod]
    [DataRow("""{"fint32":3}""")]
    [DataRow("""{"renamedd":3}""")]
    public void TryParse_UnknownMember_IsUnknownField(string json)
    {
        var error = Parse("Scalars", json, out var message);

        Assert.AreEqual(ProtobufJsonError.UnknownField, error);
        Assert.IsNull(message);
    }

    [TestMethod]
    [DataRow("fInt64", "\"-9223372036854775808\"", long.MinValue)]
    [DataRow("fInt64", "42", 42L)]
    [DataRow("fSint64", "\"-8\"", -8L)]
    [DataRow("fSfixed64", "-12", -12L)]
    [DataRow("fUint64", "\"18446744073709551615\"", ulong.MaxValue)]
    [DataRow("fFixed64", "\"10\"", 10UL)]
    [DataRow("fInt32", "\"-3\"", -3)]
    [DataRow("fInt32", "1.0", 1)]
    [DataRow("fInt32", "1e2", 100)]
    [DataRow("fSint32", "-7", -7)]
    [DataRow("fSfixed32", "-11", -11)]
    [DataRow("fUint32", "4294967295", uint.MaxValue)]
    [DataRow("fFixed32", "\"9\"", 9U)]
    public void TryParse_IntegerAsNumberOrString_IsRead(string member, string json, object expected)
    {
        var message = Parse("Scalars", $$"""{"{{member}}":{{json}}}""");

        Assert.AreEqual(expected, message.GetValue(ProtobufJsonNames.FindField(message.Type, member)!.Name));
    }

    [TestMethod]
    [DataRow("fInt32", "2147483648")]
    [DataRow("fInt32", "1.5")]
    [DataRow("fInt32", "\"three\"")]
    [DataRow("fInt32", "\" 3\"")]
    [DataRow("fUint32", "-1")]
    [DataRow("fInt64", "\"9223372036854775808\"")]
    [DataRow("fUint64", "\"18446744073709551616\"")]
    [DataRow("fInt64", "1e400")]
    public void TryParse_IntegerOutOfRangeOrNotIntegral_IsInvalidValue(string member, string json)
    {
        Assert.AreEqual(ProtobufJsonError.InvalidValue, ParseError("Scalars", $$"""{"{{member}}":{{json}}}"""));
    }

    [TestMethod]
    [DataRow("\"NaN\"", double.NaN)]
    [DataRow("\"Infinity\"", double.PositiveInfinity)]
    [DataRow("\"-Infinity\"", double.NegativeInfinity)]
    [DataRow("1.5", 1.5)]
    [DataRow("\"-2.25\"", -2.25)]
    public void TryParse_Double_ReadsNumbersAndNonFiniteNames(string json, double expected)
    {
        var message = Parse("Scalars", $$"""{"fDouble":{{json}}}""");

        Assert.AreEqual(expected, message.GetValue("f_double"));
    }

    [TestMethod]
    [DataRow("\"Infinity\"", float.PositiveInfinity)]
    [DataRow("2.5", 2.5f)]
    public void TryParse_Float_ReadsNumbersAndNonFiniteNames(string json, float expected)
    {
        var message = Parse("Scalars", $$"""{"fFloat":{{json}}}""");

        Assert.AreEqual(expected, message.GetValue("f_float"));
    }

    [TestMethod]
    [DataRow("fDouble", "1e400")]
    [DataRow("fDouble", "\"nan\"")]
    [DataRow("fDouble", "\"abc\"")]
    [DataRow("fFloat", "1e39")]
    [DataRow("fFloat", "\"1e400\"")]
    public void TryParse_FloatingPointOutOfRange_IsInvalidValue(string member, string json)
    {
        Assert.AreEqual(ProtobufJsonError.InvalidValue, ParseError("Scalars", $$"""{"{{member}}":{{json}}}"""));
    }

    [TestMethod]
    public void TryParse_BoolAndString_AreRead()
    {
        var message = Parse("Scalars", """{"fBool":true,"fString":"café"}""");

        Assert.IsTrue((bool)message.GetValue("f_bool"));
        Assert.AreEqual("café", message.GetValue("f_string"));
    }

    [TestMethod]
    [DataRow("\"AQID\"", new byte[] { 1, 2, 3 })]
    [DataRow("\"AQI=\"", new byte[] { 1, 2 })]
    [DataRow("\"AQI\"", new byte[] { 1, 2 })]
    [DataRow("\"-_8\"", new byte[] { 0xFB, 0xFF })]
    [DataRow("\"+/8=\"", new byte[] { 0xFB, 0xFF })]
    public void TryParse_Bytes_ReadsStandardOrUrlSafeBase64WithOrWithoutPadding(string json, byte[] expected)
    {
        var message = Parse("Scalars", $$"""{"fBytes":{{json}}}""");

        CollectionAssert.AreEqual(expected, (byte[])message.GetValue("f_bytes"));
    }

    [TestMethod]
    [DataRow("fBytes", "\"A\"")]
    [DataRow("fBytes", "\"!!!!\"")]
    [DataRow("fColor", "\"BLUE\"")]
    [DataRow("fColor", "2147483648")]
    public void TryParse_BytesNotBase64OrUnknownEnumName_IsInvalidValue(string member, string json)
    {
        Assert.AreEqual(ProtobufJsonError.InvalidValue, ParseError("Scalars", $$"""{"{{member}}":{{json}}}"""));
    }

    [TestMethod]
    [DataRow("\"GREEN\"", 1)]
    [DataRow("1", 1)]
    [DataRow("7", 7)]
    public void TryParse_Enum_ReadsNameOrNumber(string json, int expected)
    {
        var message = Parse("Scalars", $$"""{"fColor":{{json}}}""");

        Assert.AreEqual(expected, message.GetValue("f_color"));
    }

    [TestMethod]
    [DataRow("Scalars", """{"fInt32":true}""")]
    [DataRow("Scalars", """{"fDouble":[]}""")]
    [DataRow("Scalars", """{"fBool":"true"}""")]
    [DataRow("Scalars", """{"fString":1}""")]
    [DataRow("Scalars", """{"fBytes":1}""")]
    [DataRow("Scalars", """{"fColor":true}""")]
    [DataRow("Presence", """{"child":"x"}""")]
    [DataRow("Lists", """{"numbers":1}""")]
    [DataRow("Lists", """{"numbers":[1,null]}""")]
    [DataRow("Maps", """{"counts":[]}""")]
    [DataRow("Maps", """{"counts":{"a":true}}""")]
    [DataRow("Scalars", "[]")]
    [DataRow("Scalars", "1")]
    public void TryParse_ValueOfTheWrongKind_IsWrongType(string name, string json)
    {
        var error = Parse(name, json, out var message);

        Assert.AreEqual(ProtobufJsonError.WrongType, error);
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryParse_NullMember_LeavesTheFieldUnset()
    {
        var message = Parse("Presence", """{"count":1,"count":null,"child":null}""");

        Assert.IsFalse(message.HasValue("count"));
        Assert.IsFalse(message.HasValue("child"));
    }

    [TestMethod]
    public void TryParse_EmbeddedMessage_IsRead()
    {
        var message = Parse("Presence", """{"child":{"fInt32":5}}""");

        Assert.AreEqual(5, ((DynamicMessage)message.GetValue("child")).GetValue("f_int32"));
    }

    [TestMethod]
    public void TryParse_RepeatedFields_ReadEveryElement()
    {
        var message = Parse("Lists", """{"numbers":["1",2],"items":[{"fInt32":3},{}],"colors":["GREEN",0]}""");

        CollectionAssert.AreEqual(new object[] { 1L, 2L }, message.GetRepeated("numbers").ToArray());
        Assert.AreEqual(3, ((DynamicMessage)message.GetRepeated("items")[0]).GetValue("f_int32"));
        CollectionAssert.AreEqual(new object[] { 1, 0 }, message.GetRepeated("colors").ToArray());
    }

    [TestMethod]
    public void TryParse_Maps_ReadKeysFromMemberNames()
    {
        var message = Parse("Maps", """{"counts":{"a":1},"items":{"-5":{"fInt32":3}},"flags":{"true":"t","false":"f"},"colors":{"4294967295":"GREEN"}}""");

        Assert.AreEqual(1, message.GetMap("counts")["a"]);
        Assert.AreEqual(3, ((DynamicMessage)message.GetMap("items")[-5L]).GetValue("f_int32"));
        Assert.AreEqual("t", message.GetMap("flags")[true]);
        Assert.AreEqual("f", message.GetMap("flags")[false]);
        Assert.AreEqual(1, message.GetMap("colors")[uint.MaxValue]);
    }

    [TestMethod]
    [DataRow("""{"flags":{"maybe":"x"}}""")]
    [DataRow("""{"items":{"five":{}}}""")]
    [DataRow("""{"colors":{"-1":"GREEN"}}""")]
    public void TryParse_MapKeyNotOfTheKeyType_IsInvalidValue(string json)
    {
        Assert.AreEqual(ProtobufJsonError.InvalidValue, ParseError("Maps", json));
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("""{"fInt32":1,}""")]
    [DataRow("""{"fInt32":1} x""")]
    [DataRow("")]
    public void TryParse_MalformedJson_IsMalformedJson(string json)
    {
        var error = Parse("Scalars", json, out var message);

        Assert.AreEqual(ProtobufJsonError.MalformedJson, error);
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryParse_InvalidUtf8_IsMalformedJson()
    {
        byte[] json = [.. "{\"fString\":\""u8, 0xFF, .. "\"}"u8];

        Assert.AreEqual(ProtobufJsonError.MalformedJson, ProtobufJsonParser.TryParse(Type("Scalars"), json, out _));
    }

    [TestMethod]
    [DataRow("""{"fString":"\uD800"}""")]
    [DataRow("""{"\uD800":1}""")]
    public void TryParse_LoneSurrogateEscape_IsMalformedJson(string json)
    {
        Assert.AreEqual(ProtobufJsonError.MalformedJson, ParseError("Scalars", json));
    }

    [TestMethod]
    public void TryParse_MessagesNestedOneHundredDeep_AreRead()
    {
        var message = Parse("Node", NestedNodes(100));

        Assert.IsTrue(message.HasValue("next"));
    }

    [TestMethod]
    public void TryParse_MessagesNestedPastOneHundred_IsNestingTooDeep()
    {
        Assert.AreEqual(ProtobufJsonError.NestingTooDeep, ParseError("Node", NestedNodes(101)));
    }

    [TestMethod]
    public void TryParse_DeepJsonArrays_IsWrongTypeNotAnException()
    {
        var json = $$"""{"numbers":{{new string('[', 10_000)}}{{new string(']', 10_000)}}}""";

        Assert.AreEqual(ProtobufJsonError.WrongType, ParseError("Lists", json));
    }

    [TestMethod]
    public void TryParse_NullType_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ProtobufJsonParser.TryParse(null!, Encoding.UTF8.GetBytes("{}"), out _));
    }

    internal static string NestedNodes(int depth) =>
        string.Concat(Enumerable.Repeat("""{"next":""", depth)) + "{}" + new string('}', depth);
}
