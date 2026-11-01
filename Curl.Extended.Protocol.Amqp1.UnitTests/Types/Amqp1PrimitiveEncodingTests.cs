using System.Text;

namespace Curl.Extended.Protocol.Amqp1.Types;

[TestClass]
public sealed class Amqp1PrimitiveEncodingTests
{
    public static IEnumerable<object?[]> SmallestEncodings =>
    [
        [null, "40"],
        [true, "41"],
        [false, "42"],
        [(byte)0xAB, "50AB"],
        [(ushort)0x1234, "601234"],
        [0u, "43"],
        [255u, "52FF"],
        [256u, "7000000100"],
        [0ul, "44"],
        [7ul, "5307"],
        [0x1_0000_0000ul, "800000000100000000"],
        [(sbyte)-2, "51FE"],
        [(short)-2, "61FFFE"],
        [-128, "5480"],
        [127, "547F"],
        [128, "7100000080"],
        [-129, "71FFFFFF7F"],
        [-1L, "55FF"],
        [1000L, "8100000000000003E8"],
        [-1000L, "81FFFFFFFFFFFFFC18"],
        [1.5f, "723FC00000"],
        [1.5d, "823FF8000000000000"],
        [new Rune(0x1F600), "730001F600"],
        [new Amqp1Timestamp(1_000), "8300000000000003E8"],
        [new Guid("00112233-4455-6677-8899-aabbccddeeff"), "9800112233445566778899AABBCCDDEEFF"],
        [new byte[] { 1, 2 }, "A0020102"],
        ["hé", "A10368C3A9"],
        [new Amqp1Symbol("ab"), "A3026162"],
    ];

    public static IEnumerable<object?[]> OtherEncodings =>
    [
        ["5600", false],
        ["5601", true],
        ["7000000000", 0u],
        ["5200", 0u],
        ["8000000000000000FF", 255ul],
        ["5300", 0ul],
        ["7100000005", 5],
        ["8100000000000000FF", 255L],
        ["55FF", -1L],
        ["B00000000101", new byte[] { 1 }],
        ["B10000000161", "a"],
        ["B30000000161", new Amqp1Symbol("a")],
    ];

    [TestMethod]
    [DynamicData(nameof(SmallestEncodings))]
    public void Encode_EveryPrimitive_WritesSmallestEncodingAndReadsBack_Section1_6(object? value, string hex)
    {
        Assert.AreEqual(hex, Convert.ToHexString(Amqp1Writer.Encode(value)));

        var decoded = Amqp1Reader.Decode(Convert.FromHexString(hex));
        Assert.AreEqual(value?.GetType(), decoded?.GetType());
        Assert.AreEqual(hex, Convert.ToHexString(Amqp1Writer.Encode(decoded)));
    }

    [TestMethod]
    [DynamicData(nameof(OtherEncodings))]
    public void Decode_LargerEncodings_ReadTheSameValue_Section1_6(string hex, object expected)
    {
        var decoded = Amqp1Reader.Decode(Convert.FromHexString(hex));

        Assert.AreEqual(Convert.ToHexString(Amqp1Writer.Encode(expected)), Convert.ToHexString(Amqp1Writer.Encode(decoded)));
        Assert.AreEqual(expected.GetType(), decoded!.GetType());
    }

    [TestMethod]
    [DataRow(4, "74")]
    [DataRow(8, "84")]
    [DataRow(16, "94")]
    public void Decimal_RawBytes_WriteAndReadUnderTheirWidthCode_Sections1_6_13To1_6_15(int length, string code)
    {
        var raw = Enumerable.Range(1, length).Select(index => (byte)index).ToArray();
        var hex = code + Convert.ToHexString(raw);

        Assert.AreEqual(hex, Convert.ToHexString(Amqp1Writer.Encode(new Amqp1Decimal(raw))));
        var decoded = (Amqp1Decimal)Amqp1Reader.Decode(Convert.FromHexString(hex))!;
        CollectionAssert.AreEqual(raw, decoded.Bytes);
    }

    [TestMethod]
    public void Decimal_WidthOtherThan4Or8Or16_IsRefused() =>
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => new Amqp1Decimal(new byte[5]));

    [TestMethod]
    [DataRow(255, "A0FF", "A1FF", "A3FF")]
    [DataRow(256, "B000000100", "B100000100", "B300000100")]
    public void VariableWidth_At256Bytes_MovesTo32BitSize_Sections1_6_19To1_6_21(int length, string binary, string text, string symbol)
    {
        Assert.StartsWith(binary, Convert.ToHexString(Amqp1Writer.Encode(new byte[length])));
        Assert.StartsWith(text, Convert.ToHexString(Amqp1Writer.Encode(new string('a', length))));
        Assert.StartsWith(symbol, Convert.ToHexString(Amqp1Writer.Encode(new Amqp1Symbol(new string('a', length)))));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty input")]
    [DataRow("57", DisplayName = "unknown constructor")]
    [DataRow("710000", DisplayName = "truncated int")]
    [DataRow("A10541", DisplayName = "str8 size past the buffer")]
    [DataRow("B0FFFFFFFF", DisplayName = "vbin32 size past the buffer")]
    [DataRow("A101FF", DisplayName = "invalid UTF-8")]
    [DataRow("A30180", DisplayName = "non-ASCII symbol")]
    [DataRow("5602", DisplayName = "boolean byte other than 0 or 1")]
    [DataRow("730000D800", DisplayName = "surrogate char")]
    [DataRow("4040", DisplayName = "trailing bytes")]
    public void Decode_MalformedPrimitive_RaisesMalformedEncoding(string hex) =>
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Reader.Decode(Convert.FromHexString(hex)));

    [TestMethod]
    public void Encode_NonAsciiSymbol_RaisesMalformedEncoding() =>
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Writer.Encode(new Amqp1Symbol("é")));

    [TestMethod]
    public void Encode_TypeWithNoAmqpEncoding_RaisesMalformedEncoding() =>
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Writer.Encode(DateTime.UnixEpoch));
}
