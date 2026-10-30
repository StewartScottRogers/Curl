using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.Checksum.UnitTests;

[TestClass]
public sealed class ExpectedChecksumTests
{
    // FIPS 180-4's algorithms on NIST's worked examples ("abc", and the two-block 448-bit message for SHA-256).
    [TestMethod]
    [DataRow("abc", "sha1:a9993e364706816aba3e25717850c26c9cd0d89d", DisplayName = "FIPS 180-4 §6.1 SHA-1, one block")]
    [DataRow("abc", "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DisplayName = "FIPS 180-4 §6.2 SHA-256, one block")]
    [DataRow("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "sha256:248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1", DisplayName = "FIPS 180-4 §6.2 SHA-256, two blocks")]
    [DataRow("abc", "sha384:cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7", DisplayName = "FIPS 180-4 §6.5 SHA-384, one block")]
    [DataRow("abc", "sha512:ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f", DisplayName = "FIPS 180-4 §6.4 SHA-512, one block")]
    [DataRow("abc", "sha224:23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7", DisplayName = "FIPS 180-4 §6.3 SHA-224, one block")]
    [DataRow("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "sha224:75388b16512776cc5dba5da1fd890150b0c6455cb4f58b1952522525", DisplayName = "FIPS 180-4 §6.3 SHA-224, two blocks")]
    [DataRow("abc", "sha512-224:4634270f707b6a54daae7530460842e20e37ed265ceee9a43e8924aa", DisplayName = "FIPS 180-4 §6.7 SHA-512/224, one block")]
    [DataRow("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu", "sha512-224:23fec5bb94d60b23308192640b0c453335d664734fe40e7268674af9", DisplayName = "FIPS 180-4 §6.7 SHA-512/224, two blocks")]
    [DataRow("abc", "sha512-256:53048e2681941ef99b2e29b76b4c7dabe4c2d0c634fc6d46e0e2f13107e7af23", DisplayName = "FIPS 180-4 §6.7 SHA-512/256, one block")]
    [DataRow("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu", "sha512-256:3928e184fb8690f840da3988121d31be65cb9d3ef83ee6146feac861e19b563a", DisplayName = "FIPS 180-4 §6.7 SHA-512/256, two blocks")]
    public void TryParse_AFips1804Digest_GivesTheAlgorithmWhoseHashOfTheMessageIsThatDigest(string message, string value)
    {
        var expected = ExpectedChecksum.TryParse(value)!;

        using var hash = expected.CreateHash();
        hash.AppendData(Encoding.ASCII.GetBytes(message[..1]));
        hash.AppendData(Encoding.ASCII.GetBytes(message[1..]));
        var digest = hash.GetHashAndReset();
        hash.AppendData(Encoding.ASCII.GetBytes(message));

        CollectionAssert.AreEqual(expected.Digest, digest);
        CollectionAssert.AreEqual(digest, hash.GetHashAndReset(), "The hash did not start a new message after giving its digest.");
        Assert.AreEqual(value, expected.Format(digest));
    }

    [TestMethod]
    public void TryParse_UpperCase_ReadsTheAlgorithmAndDigitsInEitherCase()
    {
        var expected = ExpectedChecksum.TryParse("SHA1:A9993E364706816ABA3E25717850C26C9CD0D89D")!;

        Assert.AreEqual("sha1", expected.AlgorithmName);
        Assert.AreEqual("sha1:a9993e364706816aba3e25717850c26c9cd0d89d", expected.Format(expected.Digest));
    }

    [TestMethod]
    [DataRow("a9993e364706816aba3e25717850c26c9cd0d89d", DisplayName = "no algorithm")]
    [DataRow("md5:900150983cd24fb0d6963f7d28e17f72", DisplayName = "not FIPS 180-4")]
    [DataRow("sha1:a9993e364706816aba3e25717850c26c9cd0d89", DisplayName = "one digit short")]
    [DataRow("sha1:a9993e364706816aba3e25717850c26c9cd0d89dd", DisplayName = "one digit long")]
    [DataRow("sha1:g9993e364706816aba3e25717850c26c9cd0d89d", DisplayName = "not hexadecimal")]
    [DataRow("sha1:", DisplayName = "no digest")]
    [DataRow("", DisplayName = "empty")]
    public void TryParse_AnythingElse_GivesNone(string value)
    {
        Assert.IsNull(ExpectedChecksum.TryParse(value));
    }
}
