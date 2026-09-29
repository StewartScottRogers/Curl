using System.Buffers;
using System.Net.Security;

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
    public void WrapAndUnwrap_AfterAnNtlmLogon_SealForTheServerAndUnsealWhatItSeals()
    {
        using ILdapLogonAuthentication client = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");
        using var server = new NegotiateAuthentication(new NegotiateAuthenticationServerOptions { Package = "NTLM", RequiredProtectionLevel = ProtectionLevel.EncryptAndSign });
        byte[]? challenge = server.GetOutgoingBlob(client.NextToken([]), out _);
        server.GetOutgoingBlob(client.NextToken(challenge), out _);
        var reply = new ArrayBufferWriter<byte>();
        server.Wrap("done"u8, reply, requestEncryption: true, out _);
        var unsealed = new ArrayBufferWriter<byte>();

        byte[] sealedSearch = client.Wrap("search"u8);
        NegotiateAuthenticationStatusCode status = server.Unwrap(sealedSearch, unsealed, out bool encrypted);
        byte[]? unsealedReply = client.Unwrap(reply.WrittenSpan);

        Assert.AreEqual(NegotiateAuthenticationStatusCode.Completed, status);
        Assert.IsTrue(encrypted);
        CollectionAssert.AreEqual("search"u8.ToArray(), unsealed.WrittenSpan.ToArray());
        Assert.HasCount(16 + 6, sealedSearch);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0 }, sealedSearch[..4]);
        CollectionAssert.AreEqual("done"u8.ToArray(), unsealedReply);
    }

    [TestMethod]
    public void Unwrap_MessageWhoseSignatureDoesNotCheck_ReturnsNull()
    {
        using ILdapLogonAuthentication client = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");
        using var server = new NegotiateAuthentication(new NegotiateAuthenticationServerOptions { Package = "NTLM", RequiredProtectionLevel = ProtectionLevel.EncryptAndSign });
        byte[]? challenge = server.GetOutgoingBlob(client.NextToken([]), out _);
        server.GetOutgoingBlob(client.NextToken(challenge), out _);
        var reply = new ArrayBufferWriter<byte>();
        server.Wrap("done"u8, reply, requestEncryption: true, out _);
        byte[] altered = reply.WrittenSpan.ToArray();
        altered[6] ^= 0xff;

        byte[]? unsealed = client.Unwrap(altered);

        Assert.IsNull(unsealed);
    }

    [TestMethod]
    public void NextToken_ChallengeThatIsNotNtlm_ReturnsNull()
    {
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");
        authentication.NextToken([]);

        Assert.IsNull(authentication.NextToken([1, 2, 3, 4]));
    }
}
