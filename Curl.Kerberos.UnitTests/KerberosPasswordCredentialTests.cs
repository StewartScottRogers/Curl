namespace Curl.Kerberos;

/// <summary>Checks that <see cref="KerberosPasswordCredential" /> never prints its password.</summary>
[TestClass]
public sealed class KerberosPasswordCredentialTests
{
    [TestMethod]
    public void ToString_Always_HidesThePassword()
    {
        KerberosPasswordCredential credential = new(FakeKdc.Alice, FakeKdc.Password);

        string text = credential.ToString();

        Assert.AreEqual("alice@EXAMPLE.TEST (password hidden)", text);
    }
}
