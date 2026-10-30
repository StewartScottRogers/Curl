using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.UnitTests.Wire;

[TestClass]
public sealed class Amqp0FieldTableCodecTests
{
    private static readonly (Amqp0FieldValue Value, string Hex)[] ValuesAndBytes =
    [
        (Amqp0FieldValue.Boolean(true), "7401"),
        (Amqp0FieldValue.Boolean(false), "7400"),
        (Amqp0FieldValue.SignedOctet(-1), "62FF"),
        (Amqp0FieldValue.UnsignedOctet(200), "42C8"),
        (Amqp0FieldValue.SignedShort(-2), "73FFFE"),
        (Amqp0FieldValue.UnsignedShort(513), "750201"),
        (Amqp0FieldValue.SignedLong(-3), "49FFFFFFFD"),
        (Amqp0FieldValue.UnsignedLong(7), "6900000007"),
        (Amqp0FieldValue.SignedLongLong(-4), "6CFFFFFFFFFFFFFFFC"),
        (Amqp0FieldValue.Float(1.0f), "663F800000"),
        (Amqp0FieldValue.Double(1.0), "643FF0000000000000"),
        (Amqp0FieldValue.Decimal(new Amqp0Decimal(2, 12345)), "440200003039"),
        (Amqp0FieldValue.LongString("hi"), "53000000026869"),
        (Amqp0FieldValue.Array([Amqp0FieldValue.SignedLong(1), Amqp0FieldValue.Void()]), "41000000064900000001" + "56"),
        (Amqp0FieldValue.Timestamp(DateTimeOffset.FromUnixTimeSeconds(1700000000)), "54000000006553F100"),
        (Amqp0FieldValue.Table(new Amqp0FieldTable([new("a", Amqp0FieldValue.Void())])), "4600000003016156"),
        (Amqp0FieldValue.Void(), "56"),
        (Amqp0FieldValue.ByteArray([1, 2]), "78000000020102"),
    ];

    [TestMethod]
    public void Encode_EveryFieldType_WritesNameTagAndValue()
    {
        foreach ((Amqp0FieldValue value, string hex) in ValuesAndBytes)
        {
            byte[] encoded = Amqp0FieldTableCodec.Encode(new Amqp0FieldTable([new("k", value)]));

            Assert.AreEqual("016B" + hex, Convert.ToHexString(encoded), value.Type.ToString());
        }
    }

    [TestMethod]
    public void Decode_EveryFieldType_ReadsTheValueBack()
    {
        foreach ((Amqp0FieldValue value, string hex) in ValuesAndBytes)
        {
            Amqp0FieldTable table = Amqp0FieldTableCodec.Decode(Convert.FromHexString("016B" + hex));

            Amqp0FieldValue read = table.Find("k")!;
            Assert.AreEqual(value.Type, read.Type);
            Assert.AreEqual("016B" + hex, Convert.ToHexString(Amqp0FieldTableCodec.Encode(table)));
        }
    }

    [TestMethod]
    public void Decode_ScalarValues_HaveTheirClrTypes()
    {
        Amqp0FieldTable table = Amqp0FieldTableCodec.Decode(Convert.FromHexString("0161" + "7401" + "0162" + "440200003039" + "0163" + "53000000026869"));

        Assert.AreEqual(true, table.Find("a")!.Value);
        Assert.AreEqual(new Amqp0Decimal(2, 12345), table.Find("b")!.Value);
        CollectionAssert.AreEqual("hi"u8.ToArray(), (byte[])table.Find("c")!.Value!);
        Assert.IsNull(table.Find("d"));
    }

    [TestMethod]
    public void FieldTable_InAWriter_IsALongString()
    {
        var writer = new Amqp0Writer();
        writer.WriteFieldTable(new Amqp0FieldTable([new("k", Amqp0FieldValue.Void())]));
        writer.WriteFieldTable(Amqp0FieldTable.Empty);

        byte[] bytes = writer.ToArray();
        Assert.AreEqual("00000003016B56" + "00000000", Convert.ToHexString(bytes));
        Assert.HasCount(1, new Amqp0Reader(bytes).ReadFieldTable().Fields);
    }

    [TestMethod]
    [DataRow("016B3F")]
    [DataRow("016B41000000013F")]
    public void Decode_UnknownFieldType_IsSyntaxError(string hex)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0FieldTableCodec.Decode(Convert.FromHexString(hex)));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    public void Decode_TruncatedValue_IsSyntaxError()
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0FieldTableCodec.Decode(Convert.FromHexString("016B4900")));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }
}
