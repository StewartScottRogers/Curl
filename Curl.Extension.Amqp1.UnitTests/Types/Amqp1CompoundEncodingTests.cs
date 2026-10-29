namespace Curl.Extension.Amqp1.Types;

[TestClass]
public sealed class Amqp1CompoundEncodingTests
{
    [TestMethod]
    public void Encode_EmptyList_IsList0_Section1_6_22()
    {
        Assert.AreEqual("45", Convert.ToHexString(Amqp1Writer.Encode(Array.Empty<object?>())));

        Assert.IsEmpty((IReadOnlyList<object?>)Amqp1Reader.Decode(new byte[] { 0x45 })!);
    }

    [TestMethod]
    public void Encode_NestedListMapAndArray_PinsBytesAndReadsBack_Sections1_6_22To1_6_24()
    {
        var map = new Amqp1Map([new(new Amqp1Symbol("k"), 1u), new(null, true)]);
        var array = new Amqp1Array(Amqp1FormatCode.Int, [1, -1]);
        object?[] list = [map, array, "x"];
        const string hex = "C01A03" + "C10804A3016B52014041" + "E00A0271" + "00000001FFFFFFFF" + "A10178";

        var encoded = Convert.ToHexString(Amqp1Writer.Encode(list));
        Assert.AreEqual(hex, encoded);

        var decoded = (IReadOnlyList<object?>)Amqp1Reader.Decode(Convert.FromHexString(hex))!;
        Assert.HasCount(3, decoded);
        var decodedMap = (Amqp1Map)decoded[0]!;
        Assert.AreEqual(new Amqp1Symbol("k"), decodedMap.Entries[0].Key);
        Assert.AreEqual(1u, decodedMap.Entries[0].Value);
        var decodedArray = (Amqp1Array)decoded[1]!;
        Assert.AreEqual(Amqp1FormatCode.Int, decodedArray.ElementFormatCode);
        CollectionAssert.AreEqual(new object[] { 1, -1 }, decodedArray.Elements.ToArray());
        Assert.IsNull(decodedArray.ElementDescriptor);
        Assert.AreEqual("x", decoded[2]);
    }

    [TestMethod]
    public void Encode_CompoundsPast255Bytes_Use32BitSizeAndCount_Sections1_6_22To1_6_25()
    {
        var big = new byte[300];

        Assert.StartsWith("D000000135" + "00000001", Convert.ToHexString(Amqp1Writer.Encode(new object?[] { big })));
        Assert.StartsWith("D100000136" + "00000002", Convert.ToHexString(Amqp1Writer.Encode(new Amqp1Map([new(null, big)]))));
        Assert.StartsWith("F000000135" + "00000001" + "B0", Convert.ToHexString(Amqp1Writer.Encode(new Amqp1Array(Amqp1FormatCode.VBin32, [big]))));
    }

    [TestMethod]
    [DataRow("D00000000500000001" + "41", "C0020141", DisplayName = "list32")]
    [DataRow("D10000000600000002" + "4041", "C103024041", DisplayName = "map32")]
    [DataRow("F00000000600000001" + "5601", "E003015601", DisplayName = "array32")]
    public void Decode_32BitCompoundHoldingLittle_ReadsBackInSmallestForm(string hex, string smallest)
    {
        var decoded = Amqp1Reader.Decode(Convert.FromHexString(hex));

        Assert.AreEqual(smallest, Convert.ToHexString(Amqp1Writer.Encode(decoded)));
    }

    [TestMethod]
    public void Encode_DescribedTypes_SymbolicAndNumericDescriptors_Section1_3()
    {
        var numeric = new Amqp1Described(0x10ul, new object?[] { "c" });
        var symbolic = new Amqp1Described(new Amqp1Symbol("amqp:open:list"), null);

        Assert.AreEqual("005310C00401A10163", Convert.ToHexString(Amqp1Writer.Encode(numeric)));
        Assert.AreEqual("00A30E616D71703A6F70656E3A6C69737440", Convert.ToHexString(Amqp1Writer.Encode(symbolic)));

        var decoded = (Amqp1Described)Amqp1Reader.Decode(Convert.FromHexString("005310C00401A10163"))!;
        Assert.AreEqual(0x10ul, decoded.Descriptor);
        Assert.AreEqual("c", ((IReadOnlyList<object?>)decoded.Value!)[0]);
    }

    [TestMethod]
    public void Encode_ArrayWithDescribedElementConstructor_WritesDescriptorOnce_Section1_6_24()
    {
        var array = new Amqp1Array(Amqp1FormatCode.SmallUInt, [1u, 2u], 0x07ul);
        const string hex = "E00702005307520102";

        Assert.AreEqual(hex, Convert.ToHexString(Amqp1Writer.Encode(array)));

        var decoded = (Amqp1Array)Amqp1Reader.Decode(Convert.FromHexString(hex))!;
        Assert.AreEqual(0x07ul, decoded.ElementDescriptor);
        Assert.AreEqual(Amqp1FormatCode.SmallUInt, decoded.ElementFormatCode);
        CollectionAssert.AreEqual(new object[] { 1u, 2u }, decoded.Elements.ToArray());
    }

    public static IEnumerable<object?[]> ArrayElements =>
    [
        [Amqp1FormatCode.Boolean, true, "01"],
        [Amqp1FormatCode.Boolean, false, "00"],
        [Amqp1FormatCode.UByte, (byte)1, "01"],
        [Amqp1FormatCode.UShort, (ushort)1, "0001"],
        [Amqp1FormatCode.UInt, 1u, "00000001"],
        [Amqp1FormatCode.SmallUInt, 1u, "01"],
        [Amqp1FormatCode.ULong, 1ul, "0000000000000001"],
        [Amqp1FormatCode.SmallULong, 1ul, "01"],
        [Amqp1FormatCode.Byte, (sbyte)-1, "FF"],
        [Amqp1FormatCode.Short, (short)-1, "FFFF"],
        [Amqp1FormatCode.SmallInt, -1, "FF"],
        [Amqp1FormatCode.Long, -1L, "FFFFFFFFFFFFFFFF"],
        [Amqp1FormatCode.Float, 1.5f, "3FC00000"],
        [Amqp1FormatCode.Double, 1.5d, "3FF8000000000000"],
        [Amqp1FormatCode.Char, new System.Text.Rune('a'), "00000061"],
        [Amqp1FormatCode.Timestamp, new Amqp1Timestamp(1), "0000000000000001"],
        [Amqp1FormatCode.Uuid, Guid.Empty, "00000000000000000000000000000000"],
        [Amqp1FormatCode.VBin8, new byte[] { 9 }, "0109"],
        [Amqp1FormatCode.Str8, "a", "0161"],
        [Amqp1FormatCode.Str32, "a", "0000000161"],
        [Amqp1FormatCode.Sym8, new Amqp1Symbol("a"), "0161"],
        [Amqp1FormatCode.Sym32, new Amqp1Symbol("a"), "0000000161"],
        [Amqp1FormatCode.List8, new object?[] { null }, "020140"],
        [Amqp1FormatCode.List32, new object?[] { null }, "000000050000000140"],
        [Amqp1FormatCode.Map8, new Amqp1Map([new(null, null)]), "03024040"],
        [Amqp1FormatCode.Map32, new Amqp1Map([new(null, null)]), "00000006000000024040"],
        [Amqp1FormatCode.Array8, new Amqp1Array(Amqp1FormatCode.UByte, [(byte)5]), "03015005"],
        [Amqp1FormatCode.Array32, new Amqp1Array(Amqp1FormatCode.UByte, [(byte)5]), "00000006000000015005"],
        [Amqp1FormatCode.Decimal32, new Amqp1Decimal(new byte[4]), "00000000"],
        [Amqp1FormatCode.Decimal64, new Amqp1Decimal(new byte[8]), "0000000000000000"],
        [Amqp1FormatCode.Decimal128, new Amqp1Decimal(new byte[16]), "00000000000000000000000000000000"],
    ];

    [TestMethod]
    [DynamicData(nameof(ArrayElements))]
    public void WriteBody_EveryFixedConstructor_WritesTheElementAndReadsItBack_Section1_6_24(byte formatCode, object value, string bodyHex)
    {
        var writer = new Amqp1Writer();
        writer.WriteBody(formatCode, value);
        Assert.AreEqual(bodyHex, Convert.ToHexString(writer.WrittenSpan));

        var reader = new Amqp1Reader(Convert.FromHexString(bodyHex));
        var decoded = reader.ReadBody(formatCode);
        Assert.IsTrue(reader.IsAtEnd);
        Assert.AreEqual(Convert.ToHexString(Amqp1Writer.Encode(value)), Convert.ToHexString(Amqp1Writer.Encode(decoded)));
    }

    public static IEnumerable<object?[]> UnwritableArrayElements =>
    [
        [Amqp1FormatCode.Null, null],
        [Amqp1FormatCode.UInt, 1],
        [Amqp1FormatCode.UInt, null],
        [Amqp1FormatCode.SmallUInt, 256u],
        [Amqp1FormatCode.SmallULong, 256ul],
        [Amqp1FormatCode.SmallInt, 128],
        [Amqp1FormatCode.SmallLong, -129L],
        [Amqp1FormatCode.Decimal32, new Amqp1Decimal(new byte[8])],
        [Amqp1FormatCode.VBin8, new byte[256]],
        [Amqp1FormatCode.List8, new object?[] { new byte[300] }],
        [Amqp1FormatCode.Sym8, new Amqp1Symbol("é")],
    ];

    [TestMethod]
    [DynamicData(nameof(UnwritableArrayElements))]
    public void Encode_ArrayElementTheConstructorCannotHold_RaisesMalformedEncoding(byte formatCode, object? element) =>
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Writer.Encode(new Amqp1Array(formatCode, [element])));

    [TestMethod]
    [DataRow("C00105", DisplayName = "list count past the buffer")]
    [DataRow("C0030141", DisplayName = "list size past the buffer")]
    [DataRow("C003014141", DisplayName = "list bytes after its count of values")]
    [DataRow("C1020141", DisplayName = "map with an odd count")]
    [DataRow("C10402414141", DisplayName = "map bytes after its count of values")]
    [DataRow("E0020240", DisplayName = "array count past the buffer")]
    [DataRow("E002015700", DisplayName = "array of an unknown constructor")]
    [DataRow("E00401505050", DisplayName = "array bytes after its count of elements")]
    [DataRow("D0FFFFFFFF", DisplayName = "list32 truncated size")]
    [DataRow("0053", DisplayName = "described type missing its value")]
    public void Decode_MalformedCompound_RaisesMalformedEncoding(string hex) =>
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Reader.Decode(Convert.FromHexString(hex)));

    [TestMethod]
    public void Decode_NestingPastTheLimit_RaisesMalformedEncodingNotStackOverflow()
    {
        var withinLimit = Enumerable.Repeat((byte)0x00, Amqp1Reader.MaximumNestingDepth).Concat(Enumerable.Repeat((byte)0x40, Amqp1Reader.MaximumNestingDepth + 1));
        var pastLimit = Enumerable.Repeat((byte)0x00, Amqp1Reader.MaximumNestingDepth + 1).Append((byte)0x40);

        Assert.IsNotNull(new Amqp1Reader(withinLimit.ToArray()).ReadValue());
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Reader.Decode(pastLimit.ToArray()));
    }
}
