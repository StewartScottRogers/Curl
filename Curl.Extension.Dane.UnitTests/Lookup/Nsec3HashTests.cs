using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

[TestClass]
public sealed class Nsec3HashTests
{
    private static readonly Nsec3RecordData AppendixAParameters = new(1, 1, 12, Convert.FromHexString("AABBCCDD"), [], []);

    [TestMethod]
    [DataRow("example.", "0p9mhaveqvm6t7vbl5lop2u3t2rp3tom")]
    [DataRow("a.example.", "35mthgpgcu1qg68fab165klnsnk3dpvl")]
    public void Compute_Rfc5155AppendixA_GivesTheHashedOwnerName(string name, string hashedOwnerLabel)
    {
        byte[] hash = Nsec3Hash.Compute(DnssecTestRecords.Name(name), AppendixAParameters);

        CollectionAssert.AreEqual(Nsec3Hash.DecodeBase32Hex(System.Text.Encoding.ASCII.GetBytes(hashedOwnerLabel.ToUpperInvariant())), hash);
        Assert.AreEqual(hashedOwnerLabel, SignedTree.EncodeBase32Hex(hash));
    }

    [TestMethod]
    public void DecodeBase32Hex_CharacterOutsideTheAlphabet_GivesNull_Rfc4648Section7() =>
        Assert.IsNull(Nsec3Hash.DecodeBase32Hex("0w"u8.ToArray()));
}
