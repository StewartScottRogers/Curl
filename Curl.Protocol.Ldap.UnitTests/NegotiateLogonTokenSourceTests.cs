namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="NegotiateLogonTokenSource" /> against Windows' own security packages, which
/// produce an NTLM negotiate message for the logged-on user without a domain or a network.
/// Windows only: elsewhere the packages come from GSSAPI, which a test machine may not have.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class NegotiateLogonTokenSourceTests
{
    [TestMethod]
    public void Start_Negotiate_ProducesAFirstToken()
    {
        // Raw NTLM outside a domain, SPNEGO with Kerberos inside one: only its presence is machine-neutral.
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Negotiate, "ldap/127.0.0.1");

        byte[]? token = authentication.NextToken([]);

        Assert.IsNotNull(token);
        Assert.IsNotEmpty(token);
        Assert.IsFalse(authentication.IsAuthenticated);
    }

    [TestMethod]
    public void Start_NtlmFirstToken_IsTheNtlmNegotiateMessageAskingToSignAndSeal()
    {
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");

        byte[]? token = authentication.NextToken([]);

        Assert.IsNotNull(token);
        CollectionAssert.AreEqual("NTLMSSP\0\u0001\0\0\0"u8.ToArray(), token[..12]);
        Assert.AreEqual(0x30, token[12] & 0x30);
        Assert.IsFalse(authentication.IsAuthenticated);
    }

    [TestMethod]
    public void NextToken_ChallengeThatIsNotNtlm_ReturnsNull()
    {
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");
        authentication.NextToken([]);

        Assert.IsNull(authentication.NextToken([1, 2, 3, 4]));
    }
}
