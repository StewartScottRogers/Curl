namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what <see cref="Pop3Capabilities" /> reads from a <c>CAPA</c> answer for the login
/// (BL-548).
/// </summary>
[TestClass]
public sealed class Pop3CapabilitiesTests
{
    [TestMethod]
    public void SaslMechanisms_EverySaslLine_InOrderAndAnyCase()
    {
        var capabilities = new Pop3Capabilities(["+OK", string.Empty, "SASL PLAIN  LOGIN", "USER", "sasl CRAM-MD5", "SASLX NTLM"]);

        IReadOnlyList<string> mechanisms = capabilities.SaslMechanisms;

        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN", "CRAM-MD5" }, mechanisms.ToArray());
    }

    [TestMethod]
    [DataRow("USER", true)]
    [DataRow("user", true)]
    [DataRow("TOP", false)]
    public void AdvertisesUser_LineStartingUser_InAnyCase(string line, bool expected)
    {
        var capabilities = new Pop3Capabilities(["+OK", line]);

        Assert.AreEqual(expected, capabilities.AdvertisesUser);
    }
}
