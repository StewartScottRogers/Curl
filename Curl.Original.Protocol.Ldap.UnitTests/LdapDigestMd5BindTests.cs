using System.Text;

namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins <see cref="LdapDigestMd5Bind" /> to RFC 2831 and to the BindRequest bytes of RFC 4511.</summary>
[TestClass]
public sealed class LdapDigestMd5BindTests
{
    private static readonly LdapBerWriter Writer = new(LdapDialect.OpenLdap);

    [TestMethod]
    public void InitialRequest_NamesDigestMd5WithEmptyCredentials()
    {
        byte[] request = LdapDigestMd5Bind.InitialRequest(Writer, 1);

        Assert.AreEqual(
            "301a0201016015020103040" + "0a30e040a4449474553542d4d4435" + "0400",
            Convert.ToHexStringLower(request));
    }

    [TestMethod]
    public void ResponseHex_Rfc2831Example_IsTheSpecifiedDigest()
    {
        string response = LdapDigestMd5Bind.ResponseHex(
            Encoding.UTF8, "chris", "elwood.innosoft.com", "secret", "OA6MG9tEQGm2hh", "OA6MHXh6VqTrRk", "imap/elwood.innosoft.com");

        Assert.AreEqual("d388dad90d4bbd760a152321f2143af7", response);
    }

    [TestMethod]
    public void AnswerRequest_Rfc2831Challenge_SendsTheDigestAndNotThePassword()
    {
        byte[] challenge = Encoding.ASCII.GetBytes("realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8");

        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 2, challenge, "chris", "secret", "imap/elwood.innosoft.com", "OA6MHXh6VqTrRk");

        string text = "username=\"chris\",realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"OA6MHXh6VqTrRk\",nc=00000001,qop=auth,digest-uri=\"imap/elwood.innosoft.com\",response=d388dad90d4bbd760a152321f2143af7,charset=utf-8";
        Assert.IsNotNull(request);
        Assert.IsTrue(request.AsSpan(0, request.AsSpan().IndexOf("DIGEST-MD5"u8)).Contains((byte)0xa3), "SASL authentication tag");
        Assert.IsTrue(request.AsSpan().IndexOf("DIGEST-MD5"u8) > 0);
        Assert.IsTrue(request.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) > 0);
        Assert.AreEqual(-1, request.AsSpan().IndexOf("secret"u8));
    }

    [TestMethod]
    public void AnswerRequest_NoCharset_HashesAndSendsLatin1WithoutCharsetDirective()
    {
        byte[] challenge = Encoding.ASCII.GetBytes("nonce=\"n\\\"1\", qop=auth,realm=\"r\",realm=\"ignored\",junk");

        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 1, challenge, "usér", "p", "ldap/h", "c");

        Assert.IsNotNull(request);
        string sent = Encoding.Latin1.GetString(request);
        StringAssert.Contains(sent, "username=\"usér\",realm=\"r\",nonce=\"n\\\"1\",cnonce=\"c\",nc=00000001,qop=auth,digest-uri=\"ldap/h\",response=");
        Assert.IsFalse(sent.Contains("charset", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AnswerRequest_NoRealmUnquotedNonceTrailingBackslash_SendsEmptyRealm()
    {
        byte[] challenge = Encoding.ASCII.GetBytes("nonce=abc,x=\"y\",z=");

        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 1, challenge, "u", "p", "ldap/h", "c");

        Assert.IsNotNull(request);
        StringAssert.Contains(Encoding.ASCII.GetString(request), "realm=\"\",nonce=\"abc\"");
    }

    [TestMethod]
    [DataRow("realm=\"r\",qop=\"auth\"", DisplayName = "No nonce")]
    [DataRow("nonce=\"n\",qop=\"auth-int\"", DisplayName = "qop without auth")]
    [DataRow("", DisplayName = "Empty challenge")]
    public void AnswerRequest_ChallengeItCannotAnswer_ReturnsNull(string challenge)
    {
        Assert.IsNull(LdapDigestMd5Bind.AnswerRequest(Writer, 1, Encoding.ASCII.GetBytes(challenge), "u", "p", "ldap/h", "c"));
    }

    [TestMethod]
    public void AnswerRequest_UnterminatedQuotedValueEndingInBackslash_StillAnswers()
    {
        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 1, Encoding.ASCII.GetBytes("nonce=\"abc\",x=\"y\\"), "u", "p", "ldap/h", "c");

        Assert.IsNotNull(request);
    }
}
