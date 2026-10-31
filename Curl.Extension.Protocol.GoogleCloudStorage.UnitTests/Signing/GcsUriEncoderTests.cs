namespace Curl.Extension.Protocol.GoogleCloudStorage.Signing;

[TestClass]
public sealed class GcsUriEncoderTests
{
    [TestMethod]
    public void Encode_UnreservedCharacters_AreKept() =>
        Assert.AreEqual("AZaz09-._~", GcsUriEncoder.Encode("AZaz09-._~"));

    [TestMethod]
    public void Encode_ReservedAndNonAsciiCharacters_AreUpperCaseHexOfTheirUtf8Bytes() =>
        Assert.AreEqual("a%2Fb%20c%2B%C3%A9", GcsUriEncoder.Encode("a/b c+é"));

    [TestMethod]
    public void EncodePath_Path_KeepsEachSlashAndEncodesEachSegment() =>
        Assert.AreEqual("/bucket/cat%20pics/a%24b", GcsUriEncoder.EncodePath("/bucket/cat pics/a$b"));

    [TestMethod]
    public void EncodeAndEncodePath_Null_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GcsUriEncoder.Encode(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => GcsUriEncoder.EncodePath(null!));
    }
}
