using System.Text;

namespace Curl.Extension.ObliviousHttp.BinaryHttp;

[TestClass]
public sealed class BinaryHttpResponseDecoderTests
{
    [TestMethod]
    public void Decode_Rfc9292Figure11_GivesTheInformationalResponses()
    {
        byte[] message = Convert.FromHexString(Rfc9292Examples.IndeterminateLengthResponse);

        BinaryHttpResponse response = BinaryHttpResponseDecoder.Decode(message);

        Assert.HasCount(2, response.InformationalResponses);
        Assert.AreEqual(102, response.InformationalResponses[0].StatusCode);
        CollectionAssert.AreEqual(
            new BinaryHttpField[] { new("running", "\"sleep 15\"") },
            response.InformationalResponses[0].Headers.ToArray());
        Assert.AreEqual(103, response.InformationalResponses[1].StatusCode);
        CollectionAssert.AreEqual(
            new BinaryHttpField[]
            {
                new("link", "</style.css>; rel=preload; as=style"),
                new("link", "</script.js>; rel=preload; as=script"),
            },
            response.InformationalResponses[1].Headers.ToArray());
    }

    [TestMethod]
    public void Decode_Rfc9292Figure11_GivesTheFinalStatusHeadersAndContent()
    {
        byte[] message = Convert.FromHexString(Rfc9292Examples.IndeterminateLengthResponse);

        BinaryHttpResponse response = BinaryHttpResponseDecoder.Decode(message);

        Assert.AreEqual(200, response.StatusCode);
        CollectionAssert.AreEqual(
            new BinaryHttpField[]
            {
                new("date", "Mon, 27 Jul 2009 12:28:53 GMT"),
                new("server", "Apache"),
                new("last-modified", "Wed, 22 Jul 2009 19:15:56 GMT"),
                new("etag", "\"34aa387-d-1568eb00\""),
                new("accept-ranges", "bytes"),
                new("content-length", "51"),
                new("vary", "Accept-Encoding"),
                new("content-type", "text/plain"),
            },
            response.Headers.ToArray());
        Assert.AreEqual("Hello World! My content includes a trailing CRLF.\r\n", Encoding.ASCII.GetString(response.Content.Span));
        Assert.IsEmpty(response.Trailers);
    }

    [TestMethod]
    public void Decode_Rfc9292Figure13_GivesTheStatusContentAndTrailer()
    {
        byte[] message = Convert.FromHexString(Rfc9292Examples.KnownLengthResponse);

        BinaryHttpResponse response = BinaryHttpResponseDecoder.Decode(message);

        Assert.IsEmpty(response.InformationalResponses);
        Assert.AreEqual(200, response.StatusCode);
        Assert.IsEmpty(response.Headers);
        Assert.AreEqual("This content contains CRLF.\r\n", Encoding.ASCII.GetString(response.Content.Span));
        CollectionAssert.AreEqual(new BinaryHttpField[] { new("trailer", "text") }, response.Trailers.ToArray());
    }

    [TestMethod]
    public void Decode_KnownLengthWithAnInformationalResponse_GivesItBeforeTheFinalResponse()
    {
        // Framing 1; 103 with "link: <a>"; 204 with no header fields, no content and no trailers.
        byte[] message = Convert.FromHexString("01" + "4067" + "09" + "046c696e6b" + "033c613e" + "40cc" + "00" + "00" + "00");

        BinaryHttpResponse response = BinaryHttpResponseDecoder.Decode(message);

        Assert.HasCount(1, response.InformationalResponses);
        Assert.AreEqual(103, response.InformationalResponses[0].StatusCode);
        CollectionAssert.AreEqual(
            new BinaryHttpField[] { new("link", "<a>") },
            response.InformationalResponses[0].Headers.ToArray());
        Assert.AreEqual(204, response.StatusCode);
        Assert.IsTrue(response.Content.IsEmpty);
    }

    [TestMethod]
    [DataRow("0140c800", DisplayName = "Known length, truncated after the header section")]
    [DataRow("0140c80000", DisplayName = "Known length, truncated after the content")]
    [DataRow("0340c800", DisplayName = "Indeterminate length, truncated after the header section")]
    [DataRow("0340c80000", DisplayName = "Indeterminate length, truncated after the content")]
    [DataRow("0140c800000000000000", DisplayName = "Known length, padded")]
    [DataRow("0340c8000000000000", DisplayName = "Indeterminate length, padded")]
    public void Decode_TruncatedOrPaddedAfterTheHeaderSection_GivesEmptyContentAndTrailers(string message)
    {
        byte[] input = Convert.FromHexString(message);

        BinaryHttpResponse response = BinaryHttpResponseDecoder.Decode(input);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.Content.IsEmpty);
        Assert.IsEmpty(response.Trailers);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("01", DisplayName = "Ends before the status code")]
    [DataRow("0140c8", DisplayName = "Known length, ends before the header section")]
    [DataRow("0340c8", DisplayName = "Indeterminate length, ends before the header section")]
    [DataRow("0340c8046461", DisplayName = "Indeterminate length, ends inside a field name")]
    [DataRow("0340c80003414243", DisplayName = "Indeterminate length, ends before the content terminator")]
    [DataRow("0340c80000046461", DisplayName = "Indeterminate length, ends inside a trailer")]
    public void Decode_TruncatedMessage_ThrowsBinaryHttpFormatException(string message)
    {
        byte[] input = Convert.FromHexString(message);

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => BinaryHttpResponseDecoder.Decode(input));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(4)]
    public void Decode_FramingIndicatorNotAResponses_ThrowsBinaryHttpFormatException(int framingIndicator)
    {
        byte[] input = [(byte)framingIndicator, 0x40, 0xc8, 0x00, 0x00, 0x00];

        BinaryHttpFormatException exception =
            Assert.ThrowsExactly<BinaryHttpFormatException>(() => BinaryHttpResponseDecoder.Decode(input));

        Assert.AreEqual($"Binary HTTP framing indicator {framingIndicator} is not a response's (1 or 3).", exception.Message);
    }

    [TestMethod]
    [DataRow("0140c87fff00", DisplayName = "Known length, header section longer than the message")]
    [DataRow("0140c8007fff00", DisplayName = "Known length, content longer than the message")]
    [DataRow("0140c803014105", DisplayName = "Known length, field value longer than its section")]
    [DataRow("0340c87fff41", DisplayName = "Indeterminate length, field name longer than the message")]
    [DataRow("0340c8007fff41", DisplayName = "Indeterminate length, chunk longer than the message")]
    public void Decode_LengthLongerThanTheMessage_ThrowsBinaryHttpFormatException(string message)
    {
        byte[] input = Convert.FromHexString(message);

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => BinaryHttpResponseDecoder.Decode(input));
    }

    [TestMethod]
    [DataRow("4063", DisplayName = "99")]
    [DataRow("4258", DisplayName = "600")]
    public void Decode_StatusCodeOutOfRange_ThrowsBinaryHttpFormatException(string statusCode)
    {
        byte[] input = Convert.FromHexString("01" + statusCode + "000000");

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => BinaryHttpResponseDecoder.Decode(input));
    }

    [TestMethod]
    public void Decode_KnownLengthFieldWithAnEmptyName_ThrowsBinaryHttpFormatException()
    {
        byte[] input = Convert.FromHexString("0140c802" + "0000" + "0000");

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => BinaryHttpResponseDecoder.Decode(input));
    }

    [TestMethod]
    public void Decode_NonZeroPadding_ThrowsBinaryHttpFormatException()
    {
        byte[] input = Convert.FromHexString("0140c8000000000001");

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => BinaryHttpResponseDecoder.Decode(input));
    }
}
