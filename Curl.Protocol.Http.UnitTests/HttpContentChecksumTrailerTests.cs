namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContentChecksumTrailer" /> against the trailers of <c>hello</c> as
/// gzip (CRC-32 <c>3610A686</c>, size 5) and as zlib (Adler-32 <c>062C0215</c>), the bodies
/// curl 8.21.0 was measured with (BL-177 and BL-281 Notes).
/// </summary>
[TestClass]
public sealed class HttpContentChecksumTrailerTests
{
    [TestMethod]
    [DataRow(true, 7, DisplayName = "gzip: CRC-32 and size")]
    [DataRow(false, 3, DisplayName = "zlib: Adler-32")]
    public void CarriedLength_IsOneLessThanTheTrailer(bool isGzip, int expected)
    {
        HttpContentChecksumTrailer trailer = new(isGzip);

        Assert.AreEqual(expected, trailer.CarriedLength);
    }

    [TestMethod]
    [DataRow(true, HttpContentDecoderTests.Gzip, DisplayName = "gzip")]
    [DataRow(false, HttpContentDecoderTests.ZLib, DisplayName = "zlib")]
    public void EndIn_TrailerAtTheEnd_ReturnsTheWholeLength(bool isGzip, string encoded)
    {
        HttpContentChecksumTrailer trailer = Hello(isGzip);
        byte[] bytes = HttpContentDecoderTests.Bytes(encoded);

        int end = trailer.EndIn([], bytes);

        Assert.AreEqual(bytes.Length, end);
    }

    [TestMethod]
    [DataRow(true, HttpContentDecoderTests.Gzip, DisplayName = "gzip")]
    [DataRow(false, HttpContentDecoderTests.ZLib, DisplayName = "zlib")]
    public void EndIn_TrailerStartedInTheCarriedBytes_ReturnsWhereItEndsInTheEncodedBytes(bool isGzip, string encoded)
    {
        HttpContentChecksumTrailer trailer = Hello(isGzip);
        byte[] bytes = HttpContentDecoderTests.Bytes(encoded + "4142");
        int split = bytes.Length - 4;

        int end = trailer.EndIn(bytes.AsSpan(split - trailer.CarriedLength, trailer.CarriedLength), bytes.AsSpan(split));

        Assert.AreEqual(2, end);
    }

    [TestMethod]
    public void EndIn_TrailerTwice_ReturnsTheFirst()
    {
        HttpContentChecksumTrailer trailer = Hello(isGzip: true);
        byte[] bytes = HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip + HttpContentDecoderTests.Gzip);

        int end = trailer.EndIn([], bytes);

        Assert.AreEqual(25, end);
    }

    [TestMethod]
    public void EndIn_NoTrailer_ReturnsMinusOne()
    {
        HttpContentChecksumTrailer trailer = Hello(isGzip: false);

        int end = trailer.EndIn([0x06, 0x2C], HttpContentDecoderTests.Bytes("0241"));

        Assert.AreEqual(-1, end);
    }

    /// <summary>
    /// The Adler-32 sums are reduced every 5552 bytes; 100000 bytes of 0xFF cross that many
    /// times. Its Adler-32, from zlib, is <c>149A302C</c> and its CRC-32 <c>68C6CEC4</c>.
    /// </summary>
    [TestMethod]
    [DataRow(false, "149A302C", DisplayName = "zlib")]
    [DataRow(true, "C4CEC668A0860100", DisplayName = "gzip")]
    public void Append_ManyBytes_GivesTheChecksumZlibGives(bool isGzip, string expectedTrailer)
    {
        HttpContentChecksumTrailer trailer = new(isGzip);
        byte[] decoded = [.. Enumerable.Repeat((byte)0xFF, 100000)];

        trailer.Append(decoded.AsSpan(0, 30000));
        trailer.Append(decoded.AsSpan(30000));

        Assert.AreEqual(expectedTrailer.Length / 2, trailer.EndIn([], HttpContentDecoderTests.Bytes(expectedTrailer)));
    }

    private static HttpContentChecksumTrailer Hello(bool isGzip)
    {
        HttpContentChecksumTrailer trailer = new(isGzip);
        trailer.Append("hello"u8);
        return trailer;
    }
}
