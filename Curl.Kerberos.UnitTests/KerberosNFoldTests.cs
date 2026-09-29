using System.Text;

namespace Curl.Kerberos;

/// <summary>Checks <see cref="KerberosNFold" /> against RFC 3961 appendix A.1's n-fold vectors.</summary>
[TestClass]
public sealed class KerberosNFoldTests
{
    [TestMethod]
    [DataRow("012345", 64, "be072631276b1955")]
    [DataRow("password", 56, "78a07b6caf85fa")]
    [DataRow("Rough Consensus, and Running Code", 64, "bb6ed30870b7f0e0")]
    [DataRow("password", 168, "59e4a8ca7c0385c3c37b3f6d2000247cb6e6bd5b3e")]
    [DataRow("MASSACHVSETTS INSTITVTE OF TECHNOLOGY", 192, "db3b0d8f0b061e603282b308a50841229ad798fab9540c1b")]
    [DataRow("Q", 168, "518a54a2 15a8452a 518a54a2 15a8452a 518a54a2 15")]
    [DataRow("ba", 168, "fb25d531 ae897449 9f52fd92 ea9857c4 ba24cf29 7e")]
    [DataRow("kerberos", 64, "6b657262 65726f73")]
    [DataRow("kerberos", 128, "6b657262 65726f73 7b9b5b2b 93132b93")]
    [DataRow("kerberos", 168, "8372c236 344e5f15 50cd0747 e15d62ca 7a5a3bce a4")]
    [DataRow("kerberos", 256, "6b657262 65726f73 7b9b5b2b 93132b93 5c9bdcda d95c9899 c4cae4de e6d6cae4")]
    public void Fold_Rfc3961AppendixA1_MatchesVector(string input, int bits, string expected)
    {
        byte[] output = new byte[bits / 8];

        KerberosNFold.Fold(Encoding.ASCII.GetBytes(input), output);

        CollectionAssert.AreEqual(Hex.Bytes(expected), output);
    }
}
