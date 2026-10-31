namespace Curl.Extension.Protocol.S3.Signing;

/// <summary>Pins the S3 reference's "UriEncode" rules.</summary>
[TestClass]
public sealed class S3UriEncoderTests
{
    [TestMethod]
    public void Encode_UnreservedCharacters_LeavesThemAlone()
    {
        Assert.AreEqual("AZaz09-._~", S3UriEncoder.Encode("AZaz09-._~"));
    }

    [TestMethod]
    public void Encode_ReservedCharactersAndSlash_EncodesThemInUpperCaseHexadecimal()
    {
        Assert.AreEqual("%20%2F%24%3D%26%2A%2B", S3UriEncoder.Encode(" /$=&*+"));
    }

    [TestMethod]
    public void Encode_NonAsciiCharacter_EncodesEachByteOfItsUtf8Form()
    {
        Assert.AreEqual("caf%C3%A9", S3UriEncoder.Encode("café"));
    }

    [TestMethod]
    public void EncodePath_PathWithReservedCharacters_EncodesEachSegmentOnceAndKeepsTheSlashes()
    {
        Assert.AreEqual("/photos/my%20file%2520.jpg", S3UriEncoder.EncodePath("/photos/my file%20.jpg"));
    }

    [TestMethod]
    public void Encode_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => S3UriEncoder.Encode(null!));
    }

    [TestMethod]
    public void EncodePath_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => S3UriEncoder.EncodePath(null!));
    }
}
