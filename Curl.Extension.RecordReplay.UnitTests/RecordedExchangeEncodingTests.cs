using System.Text;

namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class RecordedExchangeEncodingTests
{
    [TestMethod]
    public void Encode_AnExchange_WritesTheThreeLinesThenHeadAndBody_Adr1129Section3()
    {
        var exchange = new RecordedExchange("GET", "http://example.com/r", 200, Bytes("HTTP/1.1 200 OK\r\n\r\n"), Bytes("hi"));

        var encoded = RecordedExchangeEncoding.Encode(exchange);

        Assert.AreEqual("CURL-EXCHANGE 1\nGET http://example.com/r\n200 19 2\nHTTP/1.1 200 OK\r\n\r\nhi", Encoding.Latin1.GetString(encoded));
    }

    [TestMethod]
    public void Decode_AnEncodedExchange_GivesItBack_Adr1129Section3()
    {
        var exchange = new RecordedExchange("POST", "http://example.com/a b", 201, Bytes("HTTP/1.1 201 Created\r\nX: \n\r\n"), [0, 10, 255]);

        var decoded = RecordedExchangeEncoding.Decode(RecordedExchangeEncoding.Encode(exchange))!;

        Assert.AreEqual("POST", decoded.Method);
        Assert.AreEqual("http://example.com/a b", decoded.Url);
        Assert.AreEqual(201, decoded.StatusCode);
        CollectionAssert.AreEqual(exchange.Head, decoded.Head);
        CollectionAssert.AreEqual(exchange.Body, decoded.Body);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("CURL-EXCHANGE 1\nGET u\n200 0 0")]
    [DataRow("CURL-EXCHANGE 2\nGET u\n200 0 0\n")]
    [DataRow("CURL-EXCHANGE 1\nGET\n200 0 0\n")]
    [DataRow("CURL-EXCHANGE 1\nGET u\n200 0\n")]
    [DataRow("CURL-EXCHANGE 1\nGET u\nx 0 0\n")]
    [DataRow("CURL-EXCHANGE 1\nGET u\n200 -1 0\n")]
    [DataRow("CURL-EXCHANGE 1\nGET u\n200 0 x\n")]
    [DataRow("CURL-EXCHANGE 1\nGET u\n200 1 1\nx")]
    [DataRow("CURL-EXCHANGE 1\nGET u\n200 0 1\nxy")]
    public void Decode_NotAWholeExchangeFile_GivesNone_Adr1129Section3(string text)
    {
        Assert.IsNull(RecordedExchangeEncoding.Decode(Bytes(text)));
    }

    [TestMethod]
    public void EncodeAndDecode_Null_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => RecordedExchangeEncoding.Encode(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => RecordedExchangeEncoding.Decode(null!));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
