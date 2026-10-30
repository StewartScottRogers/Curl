namespace Curl.Extension.ObliviousHttp.BinaryHttp;

[TestClass]
public sealed class BinaryHttpRequestEncoderTests
{
    [TestMethod]
    public void EncodeKnownLength_Rfc9292Figure7Request_GivesTheFigure8Bytes()
    {
        BinaryHttpRequest request = new(
            "GET",
            "https",
            "",
            "/hello.txt",
            [
                new("User-Agent", "curl/7.16.3 libcurl/7.16.3 OpenSSL/0.9.7l zlib/1.2.3"),
                new("Host", "www.example.com"),
                new("Accept-Language", "en, mi"),
            ],
            ReadOnlyMemory<byte>.Empty);

        byte[] message = BinaryHttpRequestEncoder.EncodeKnownLength(request);

        Assert.AreEqual(Rfc9292Examples.KnownLengthRequest, Convert.ToHexStringLower(message));
    }

    [TestMethod]
    public void EncodeKnownLength_RequestWithContent_WritesTheContentAfterTheHeaderSection()
    {
        BinaryHttpRequest request = new("POST", "https", "example.com", "/", [], new byte[] { 0x41, 0x42 });

        byte[] message = BinaryHttpRequestEncoder.EncodeKnownLength(request);

        Assert.AreEqual(
            "00" + "04504f5354" + "056874747073" + "0b6578616d706c652e636f6d" + "012f" + "00" + "024142" + "00",
            Convert.ToHexStringLower(message));
    }

    [TestMethod]
    public void EncodeKnownLength_EmptyFieldName_ThrowsArgumentException()
    {
        BinaryHttpRequest request = new("GET", "https", "example.com", "/", [new("", "value")], ReadOnlyMemory<byte>.Empty);

        Assert.ThrowsExactly<ArgumentException>(() => BinaryHttpRequestEncoder.EncodeKnownLength(request));
    }

    [TestMethod]
    public void EncodeKnownLength_NullRequest_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => BinaryHttpRequestEncoder.EncodeKnownLength(null!));
    }
}
