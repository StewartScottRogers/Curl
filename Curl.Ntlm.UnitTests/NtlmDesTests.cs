namespace Curl.Ntlm;

/// <summary>
/// Checks <see cref="NtlmDes.ExpandKey" /> spreads 56 key bits and sets odd parity as
/// curl's <c>extend_key_56_to_64</c> and <c>curl_des_set_odd_parity</c> do.
/// </summary>
[TestClass]
public sealed class NtlmDesTests
{
    [TestMethod]
    // All zero bits: every byte gets its parity bit.
    [DataRow("00000000000000", "0101010101010101")]
    // All one bits: seven ones each, already odd.
    [DataRow("FFFFFFFFFFFFFF", "FEFEFEFEFEFEFEFE")]
    // MS-NLMP 4.2.2.1.1's first LM key, "PASSWOR".
    [DataRow("50415353574F52", "5120546B34BA3DA4")]
    public void ExpandKey_SevenBytes_GivesTheOddParityDesKey(string sevenByteKey, string expected)
    {
        byte[] key = new byte[8];

        NtlmDes.ExpandKey(Convert.FromHexString(sevenByteKey), key);

        Assert.AreEqual(expected, Convert.ToHexString(key));
    }
}
