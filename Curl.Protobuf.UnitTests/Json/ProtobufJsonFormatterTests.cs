using Curl.Protobuf.DynamicMessages;
using static Curl.Protobuf.Json.JsonTestTypes;

namespace Curl.Protobuf.Json;

/// <summary>Pins <see cref="ProtobufJsonFormatter" /> against the rules of the proto3 JSON mapping.</summary>
[TestClass]
public sealed class ProtobufJsonFormatterTests
{
    [TestMethod]
    public void Format_EveryScalar_IsWrittenByJsonNameWith64BitIntegersAsStrings()
    {
        const string Json = """{"fDouble":1.5,"fFloat":2.5,"fInt32":-3,"fInt64":"-4","fUint32":5,"fUint64":"18446744073709551615","fSint32":-7,"fSint64":"-8","fFixed32":9,"fFixed64":"10","fSfixed32":-11,"fSfixed64":"-12","fBool":true,"fString":"café","fBytes":"AQID","fColor":"GREEN","custom":17}""";

        var formatted = RoundTrip("Scalars", Json);

        Assert.AreEqual(Json, formatted);
    }

    [TestMethod]
    public void Format_DefaultValuesWithoutPresence_AreLeftOut()
    {
        var formatted = RoundTrip("Scalars", """{"fDouble":0,"fInt64":"0","fBool":false,"fString":"","fBytes":"","fColor":"RED"}""");

        Assert.AreEqual("{}", formatted);
    }

    [TestMethod]
    public void Format_DefaultValuesWithPresence_AreWritten()
    {
        var formatted = RoundTrip("Presence", """{"count":0,"number":0,"child":{}}""");

        Assert.AreEqual("""{"count":0,"number":0,"child":{}}""", formatted);
    }

    [TestMethod]
    [DataRow("fDouble", "\"NaN\"")]
    [DataRow("fDouble", "\"Infinity\"")]
    [DataRow("fDouble", "\"-Infinity\"")]
    [DataRow("fFloat", "\"NaN\"")]
    [DataRow("fFloat", "\"-Infinity\"")]
    public void Format_NonFiniteFloatingPoint_IsWrittenByName(string member, string json)
    {
        var formatted = RoundTrip("Scalars", $$"""{"{{member}}":{{json}}}""");

        Assert.AreEqual($$"""{"{{member}}":{{json}}}""", formatted);
    }

    [TestMethod]
    public void Format_EnumNumberWithNoName_IsWrittenAsANumber()
    {
        var formatted = RoundTrip("Scalars", """{"fColor":7}""");

        Assert.AreEqual("""{"fColor":7}""", formatted);
    }

    [TestMethod]
    public void Format_RepeatedFields_AreArraysAndLeftOutWhenEmpty()
    {
        var formatted = RoundTrip("Lists", """{"numbers":[],"items":[{"fInt32":3},{}],"colors":["GREEN",5]}""");

        Assert.AreEqual("""{"items":[{"fInt32":3},{}],"colors":["GREEN",5]}""", formatted);
    }

    [TestMethod]
    public void Format_Maps_AreObjectsKeyedByText()
    {
        const string Json = """{"counts":{"a":1},"items":{"-5":{"fInt32":3}},"flags":{"true":"t","false":"f"},"colors":{"4294967295":"GREEN"}}""";

        var formatted = RoundTrip("Maps", Json);

        Assert.AreEqual(Json, formatted);
    }

    [TestMethod]
    public void Format_EmptyMap_IsLeftOut()
    {
        Assert.AreEqual("{}", RoundTrip("Maps", """{"counts":{}}"""));
    }

    [TestMethod]
    public void Format_Indented_WritesOneMemberPerLine()
    {
        var message = Parse("Scalars", """{"fInt32":1,"fBool":true}""");

        var formatted = ProtobufJsonFormatter.Format(message, indented: true);

        Assert.AreEqual("{\n  \"fInt32\": 1,\n  \"fBool\": true\n}", formatted.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void Format_NullMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ProtobufJsonFormatter.Format(null!, indented: false));
    }

    [TestMethod]
    public void Format_UnknownFields_AreDropped()
    {
        var message = new DynamicMessage(Type("Scalars"));
        message.UnknownFields.Add(new Wire.ProtobufField(99, Wire.ProtobufWireType.Varint, 1, ReadOnlyMemory<byte>.Empty, []));

        Assert.AreEqual("{}", Format(message));
    }
}
