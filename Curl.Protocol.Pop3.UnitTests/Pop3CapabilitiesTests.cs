using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what <see cref="Pop3Capabilities" /> reads from a <c>CAPA</c> answer for the login
/// (BL-548).
/// </summary>
[TestClass]
public sealed class Pop3CapabilitiesTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SaslMechanisms_EverySaslLine_InOrderAndAnyCase()
    {
        var capabilities = new Pop3Capabilities(["+OK", string.Empty, "SASL PLAIN  LOGIN", "USER", "sasl CRAM-MD5", "SASLX NTLM"]);

        Diagnostics.Arrange("capa lines", "+OK | (empty) | SASL PLAIN  LOGIN | USER | sasl CRAM-MD5 | SASLX NTLM");
        IReadOnlyList<string> mechanisms = capabilities.SaslMechanisms;
        Diagnostics.Act("sasl mechanisms", string.Join(" ", mechanisms));

        Diagnostics.Assert("sasl mechanisms", "PLAIN LOGIN CRAM-MD5", string.Join(" ", mechanisms));
        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN", "CRAM-MD5" }, mechanisms.ToArray());
    }

    [TestMethod]
    [DataRow("USER", true)]
    [DataRow("user", true)]
    [DataRow("TOP", false)]
    public void AdvertisesUser_LineStartingUser_InAnyCase(string line, bool expected)
    {
        Diagnostics.Arrange("capa line", line);
        var capabilities = new Pop3Capabilities(["+OK", line]);
        Diagnostics.Act("advertises user", capabilities.AdvertisesUser);

        Diagnostics.Assert("advertises user", expected, capabilities.AdvertisesUser);
        Assert.AreEqual(expected, capabilities.AdvertisesUser);
    }
}
