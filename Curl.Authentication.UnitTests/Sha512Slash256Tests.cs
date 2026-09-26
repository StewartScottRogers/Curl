using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="Sha512Slash256" /> to the NIST example values for SHA-512/256.
/// </summary>
[TestClass]
public sealed class Sha512Slash256Tests
{
    [TestMethod]
    [DataRow("", "c672b8d1ef56ed28ab87c3622c5114069bdd3ad7b8f9737498d0c01ecef0967a", DisplayName = "Empty")]
    [DataRow("abc", "53048e2681941ef99b2e29b76b4c7dabe4c2d0c634fc6d46e0e2f13107e7af23", DisplayName = "One block")]
    [DataRow(
        "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu",
        "3928e184fb8690f840da3988121d31be65cb9d3ef83ee6146feac861e19b563a",
        DisplayName = "112 bytes: padding needs a second block")]
    public void HashData_NistExample_GivesTheExampleHash(string message, string expected)
    {
        byte[] hash = Sha512Slash256.HashData(Encoding.ASCII.GetBytes(message));

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    [TestMethod]
    public void HashData_111Bytes_FitsOneBlock()
    {
        byte[] hash = Sha512Slash256.HashData(new byte[111]);

        Assert.HasCount(32, hash);
    }
}
