using Curl.Protobuf.Wire;
using static Curl.Protobuf.DynamicMessages.DynamicTestTypes;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// Pins <see cref="DynamicMessageEncoder" /> and <see cref="DynamicMessageDecoder" /> against
/// the byte vectors of https://protobuf.dev/programming-guides/encoding/ and proto3's rules.
/// </summary>
[TestClass]
public sealed class DynamicMessageEncoderTests
{
    [TestMethod]
    public void Encode_Int32150_IsTheSpecificationsVector()
    {
        var message = Message("Test1");
        message.SetValue("a", 150);

        Assert.AreEqual("089601", EncodeToHex(message));
        Assert.AreEqual(150, Decode("Test1", "089601").GetValue("a"));
    }

    [TestMethod]
    public void Encode_StringTesting_IsTheSpecificationsVector()
    {
        var message = Message("Test2");
        message.SetValue("b", "testing");

        Assert.AreEqual("120774657374696E67", EncodeToHex(message));
        Assert.AreEqual("testing", Decode("Test2", "120774657374696E67").GetValue("b"));
    }

    [TestMethod]
    public void Encode_NestedMessage_IsTheSpecificationsVector()
    {
        var inner = Message("Test1");
        inner.SetValue("a", 150);
        var message = Message("Test3");
        message.SetValue("c", inner);

        Assert.AreEqual("1A03089601", EncodeToHex(message));
        Assert.AreEqual(150, ((DynamicMessage)Decode("Test3", "1A03089601").GetValue("c")).GetValue("a"));
    }

    [TestMethod]
    public void Encode_RepeatedInt32_IsTheSpecificationsPackedVector()
    {
        var message = Message("Test4");
        foreach (var value in new object[] { 3, 270, 86942 })
        {
            message.GetRepeated("e").Add(value);
        }

        Assert.AreEqual("3206038E029EA705", EncodeToHex(message));
        CollectionAssert.AreEqual(new object[] { 3, 270, 86942 }, Decode("Test4", "3206038E029EA705").GetRepeated("e").ToArray());
    }

    [TestMethod]
    public void Decode_RepeatedInt32Unpacked_IsAccepted()
    {
        var message = Decode("Test4", "3003308E02309EA705");

        CollectionAssert.AreEqual(new object[] { 3, 270, 86942 }, message.GetRepeated("e").ToArray());
    }

    [TestMethod]
    public void Encode_ImplicitPresenceDefaults_WritesNothing()
    {
        var message = Message("Scalars");
        foreach (var field in message.Type.Fields)
        {
            message.SetValue(field.Name, field.CreateDefaultValue());
        }

        Assert.AreEqual(string.Empty, EncodeToHex(message));
    }

    [TestMethod]
    public void Encode_NegativeInt32AndSInt32_AreTenByteVarintAndZigZag()
    {
        var message = Message("Scalars");
        message.SetValue("f_int32", -1);
        message.SetValue("f_sint32", -1);

        Assert.AreEqual("18FFFFFFFFFFFFFFFFFF013801", EncodeToHex(message));
    }

    [TestMethod]
    public void EncodeThenDecode_EveryScalarType_KeepsEachValue()
    {
        var values = new Dictionary<string, object>
        {
            ["f_double"] = -2.5,
            ["f_float"] = 1.5f,
            ["f_int32"] = int.MinValue,
            ["f_int64"] = long.MinValue,
            ["f_uint32"] = uint.MaxValue,
            ["f_uint64"] = ulong.MaxValue,
            ["f_sint32"] = -3,
            ["f_sint64"] = -4L,
            ["f_fixed32"] = 5u,
            ["f_fixed64"] = 6UL,
            ["f_sfixed32"] = -7,
            ["f_sfixed64"] = -8L,
            ["f_bool"] = true,
            ["f_string"] = "é",
            ["f_bytes"] = new byte[] { 0, 255 },
            ["f_color"] = 1,
        };
        var message = Message("Scalars");
        foreach (var (name, value) in values)
        {
            message.SetValue(name, value);
        }

        var decoded = Decode("Scalars", EncodeToHex(message));

        foreach (var (name, value) in values)
        {
            var actual = decoded.GetValue(name);
            if (value is byte[] bytes)
            {
                CollectionAssert.AreEqual(bytes, (byte[])actual, name);
            }
            else
            {
                Assert.AreEqual(value, actual, name);
            }
        }
    }

    [TestMethod]
    public void Encode_OptionalAndOneofDefaults_AreWrittenForTheirPresence()
    {
        var message = Message("Presence");
        message.SetValue("count", 0);
        message.SetValue("text", string.Empty);

        Assert.AreEqual("08001200", EncodeToHex(message));
    }

    [TestMethod]
    public void SetValue_SecondOneofField_ClearsTheFirst()
    {
        var message = Message("Presence");
        message.SetValue("text", "a");
        message.SetValue("number", 5);

        Assert.AreEqual(("number", false), (message.GetOneofCase("choice"), message.HasValue("text")));
        Assert.AreEqual("1805", EncodeToHex(message));
    }

    [TestMethod]
    public void Decode_TwoOneofFields_KeepsTheLastOne()
    {
        var message = Decode("Presence", "1201611805");

        Assert.AreEqual(("number", 5), (message.GetOneofCase("choice"), message.GetValue("number")));
        Assert.IsNull(Message("Presence").GetOneofCase("choice"));
    }

    [TestMethod]
    public void Decode_ScalarTwice_KeepsTheLastValue()
    {
        Assert.AreEqual(2, Decode("Test1", "08010802").GetValue("a"));
    }

    [TestMethod]
    public void Decode_MessageFieldTwice_MergesThem()
    {
        var message = Decode("Outer", "0A0208011A0208071A00");

        Assert.AreEqual(7, ((DynamicMessage)message.GetValue("absolute")).GetValue("a"));
        Assert.AreEqual(1, ((DynamicMessage)message.GetValue("inner")).GetValue("color"));
    }

    [TestMethod]
    public void Decode_EnumNumberTheEnumDoesNotName_IsKept()
    {
        var message = Decode("Outer", "1005");

        Assert.AreEqual(5, message.GetValue("mood"));
        Assert.AreEqual("1005", EncodeToHex(message));
    }

    [TestMethod]
    public void DecodeThenEncode_UnknownFieldsAndWireTypeMismatch_AreKeptAndWrittenBack()
    {
        const string hex = "0A01610896011203616263150102030419010203040506070818011B08011C";

        var message = Decode("Test1", hex);

        Assert.AreEqual((150, 6), (message.GetValue("a"), message.UnknownFields.Count));
        Assert.AreEqual("0896010A0161120361626315010203041901020304050607081801" + "1B08011C", EncodeToHex(message));
    }

    [TestMethod]
    public void EncodeThenDecode_RepeatedFields_PackOnlyNumericFieldsNotMarkedUnpacked()
    {
        var message = Message("Repeats");
        message.GetRepeated("packed").Add(-1L);
        message.GetRepeated("unpacked").Add(1);
        message.GetRepeated("unpacked").Add(2);
        message.GetRepeated("names").Add("x");
        message.GetRepeated("names").Add(string.Empty);
        message.GetRepeated("items").Add(Message("Test1"));
        message.GetRepeated("fixed").Add(1u);
        message.GetRepeated("doubles").Add(1.0);

        var hex = EncodeToHex(message);
        var decoded = Decode("Repeats", hex);

        Assert.AreEqual("0A010110011002" + "1A01781A00" + "2200" + "2A0401000000" + "3208000000000000F03F", hex);
        CollectionAssert.AreEqual(new object[] { "x", string.Empty }, decoded.GetRepeated("names").ToArray());
        Assert.AreEqual(hex, EncodeToHex(decoded));
    }

    [TestMethod]
    public void Encode_EmptyRepeatedFields_WritesNothing()
    {
        var message = Message("Repeats");
        _ = message.GetRepeated("packed");
        _ = message.GetRepeated("names");

        Assert.AreEqual(string.Empty, EncodeToHex(message));
    }

    [TestMethod]
    public void EncodeThenDecode_Maps_AreRepeatedEntryMessagesWithKeyAndValueAlwaysWritten()
    {
        var item = Message("Test1");
        item.SetValue("a", 1);
        var message = Message("Maps");
        message.GetMap("counts")["a"] = 0;
        message.GetMap("items")[7] = item;

        var hex = EncodeToHex(message);
        var decoded = Decode("Maps", hex);

        Assert.AreEqual("0A050A01611000" + "1206080712020801", hex);
        Assert.AreEqual(0, decoded.GetMap("counts")["a"]);
        Assert.AreEqual(1, ((DynamicMessage)decoded.GetMap("items")[7]).GetValue("a"));
    }

    [TestMethod]
    public void Decode_MapEntriesMissingKeyOrValue_UseTheDefaults()
    {
        var message = Decode("Maps", "0A021001" + "12020807");

        Assert.AreEqual(1, message.GetMap("counts")[string.Empty]);
        Assert.IsFalse(((DynamicMessage)message.GetMap("items")[7]).HasValue("a"));
    }

    [TestMethod]
    public void Write_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DynamicMessageEncoder.Write(null!, Message("Test1")));
        Assert.ThrowsExactly<ArgumentNullException>(() => DynamicMessageEncoder.Write(new ProtobufWriter(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => DynamicMessageDecoder.TryDecode(null!, ReadOnlyMemory<byte>.Empty, out _));
    }
}
