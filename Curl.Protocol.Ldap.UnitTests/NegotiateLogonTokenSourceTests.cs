using System.Buffers;
using System.Net.Security;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Start_Negotiate_ProducesAFirstToken()
    {
        Diagnostics.Arrange("package", LdapLogonPackage.Negotiate);
        Diagnostics.Arrange("target name", "ldap/127.0.0.1");
        // Raw NTLM outside a domain, SPNEGO with Kerberos inside one: only its presence is machine-neutral.
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Negotiate, "ldap/127.0.0.1");

        byte[]? token = authentication.NextToken([]);

        Diagnostics.Act("token length", token?.Length);
        Diagnostics.Act("IsAuthenticated", authentication.IsAuthenticated);
        Diagnostics.Assert("token present", true, token is { Length: > 0 });
        Diagnostics.Assert("IsAuthenticated", false, authentication.IsAuthenticated);
        Assert.IsNotNull(token);
        Assert.IsNotEmpty(token);
        Assert.IsFalse(authentication.IsAuthenticated);
    }

    [TestMethod]
    public void Start_NtlmFirstToken_IsTheNtlmNegotiateMessageAskingToSignAndSeal()
    {
        Diagnostics.Arrange("package", LdapLogonPackage.Ntlm);
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");

        byte[]? token = authentication.NextToken([]);

        Diagnostics.Bytes("first token", token ?? []);
        Diagnostics.Act("IsAuthenticated", authentication.IsAuthenticated);
        Diagnostics.Assert("token present", true, token is not null);
        Assert.IsNotNull(token);
        Diagnostics.Assert("flags byte & 0x30", 0x30, token[12] & 0x30);
        CollectionAssert.AreEqual("NTLMSSP\0\u0001\0\0\0"u8.ToArray(), token[..12]);
        Assert.AreEqual(0x30, token[12] & 0x30);
        Assert.IsFalse(authentication.IsAuthenticated);
    }

    [TestMethod]
    public void WrapAndUnwrap_AfterAnNtlmLogon_SealForTheServerAndUnsealWhatItSeals()
    {
        Diagnostics.Arrange("package", LdapLogonPackage.Ntlm);
        Diagnostics.Arrange("client message", "search");
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

        Diagnostics.Bytes("sealed search", sealedSearch);
        Diagnostics.Act("server unwrap status", status);
        Diagnostics.Act("encrypted", encrypted);
        Diagnostics.Diff("unsealed search", "search"u8.ToArray(), unsealed.WrittenSpan.ToArray());
        Diagnostics.Diff("unsealed reply", "done"u8.ToArray(), unsealedReply ?? []);
        Diagnostics.Assert("status", NegotiateAuthenticationStatusCode.Completed, status);
        Diagnostics.Assert("sealed length", 16 + 6, sealedSearch.Length);

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
        Diagnostics.Arrange("package", LdapLogonPackage.Ntlm);
        using ILdapLogonAuthentication client = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");
        using var server = new NegotiateAuthentication(new NegotiateAuthenticationServerOptions { Package = "NTLM", RequiredProtectionLevel = ProtectionLevel.EncryptAndSign });
        byte[]? challenge = server.GetOutgoingBlob(client.NextToken([]), out _);
        server.GetOutgoingBlob(client.NextToken(challenge), out _);
        var reply = new ArrayBufferWriter<byte>();
        server.Wrap("done"u8, reply, requestEncryption: true, out _);
        byte[] altered = reply.WrittenSpan.ToArray();
        altered[6] ^= 0xff;
        Diagnostics.Bytes("altered sealed reply", altered);

        byte[]? unsealed = client.Unwrap(altered);

        Diagnostics.Act("unsealed", unsealed);
        Diagnostics.Assert("unsealed", null, unsealed);
        Assert.IsNull(unsealed);
    }

    [TestMethod]
    public void NextToken_ChallengeThatIsNotNtlm_ReturnsNull()
    {
        using ILdapLogonAuthentication authentication = new NegotiateLogonTokenSource().Start(LdapLogonPackage.Ntlm, "ldap/127.0.0.1");
        authentication.NextToken([]);
        Diagnostics.Arrange("challenge", "01 02 03 04");

        byte[]? next = authentication.NextToken([1, 2, 3, 4]);

        Diagnostics.Act("next token", next);
        Diagnostics.Assert("next token", null, next);
        Assert.IsNull(next);
    }
}
