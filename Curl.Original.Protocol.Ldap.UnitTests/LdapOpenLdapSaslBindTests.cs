using System.Net;
using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the OpenLDAP bind for <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c> and
/// <c>--login-options AUTH=</c> (BL-5435): SASL against the rootDSE's mechanisms, never a simple bind.
/// </summary>
[TestClass]
public sealed class LdapOpenLdapSaslBindTests
{
    private const string RootDseDone1 = "30 0c 02 01 01 65 07 0a 01 00 04 00 04 00";

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm, null)]
    [DataRow(HttpAuthSchemes.Negotiate, null)]
    [DataRow(HttpAuthSchemes.Digest, null)]
    [DataRow(HttpAuthSchemes.Basic, "AUTH=CRAM-MD5")]
    [DataRow(HttpAuthSchemes.Basic, "AUTH=DIGEST-MD5")]
    public async Task ExecuteAsync_ServerOffersNoAllowedMechanism_FailsWith67AndSendsNoPassword(HttpAuthSchemes schemes, string? loginOptions)
    {
        (TransferResult result, byte[] sent) = await RunAsync(schemes, loginOptions, RootDseDone1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.IsFalse(sent.AsSpan().IndexOf("secret"u8) >= 0);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest, null)]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest | HttpAuthSchemes.Ntlm | HttpAuthSchemes.Negotiate, null)]
    [DataRow(HttpAuthSchemes.Basic, "AUTH=digest-md5")]
    [DataRow(HttpAuthSchemes.Basic, "AUTH=*")]
    public async Task ExecuteAsync_ServerOffersDigestMd5_BindsWithSaslAndNoPassword(HttpAuthSchemes schemes, string? loginOptions)
    {
        (TransferResult result, byte[] sent) = await RunAsync(
            schemes,
            loginOptions,
            MechanismsEntry(1),
            RootDseDone1,
            Challenge(2, "nonce=\"abc\",qop=\"auth\""),
            "30 0c 02 01 03 61 07 0a 01 00 04 00 04 00",
            "30 0c 02 01 04 65 07 0a 01 00 04 00 04 00");

        Assert.AreEqual(TransferResult.Success(0), result, result.ToString());
        Assert.IsTrue(sent.AsSpan().IndexOf("response="u8) >= 0);
        Assert.IsFalse(sent.AsSpan().IndexOf("secret"u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerOffersAuthConfWithRc4_AnswersQopAuthAndSendsLaterPdusUnwrapped()
    {
        (TransferResult result, byte[] sent) = await RunAsync(
            HttpAuthSchemes.Digest,
            null,
            MechanismsEntry(1),
            RootDseDone1,
            Challenge(2, "nonce=\"abc\",qop=\"auth,auth-conf\",cipher=\"rc4\""),
            "30 0c 02 01 03 61 07 0a 01 00 04 00 04 00",
            "30 0c 02 01 04 65 07 0a 01 00 04 00 04 00");

        Assert.AreEqual(TransferResult.Success(0), result, result.ToString());
        Assert.IsTrue(sent.AsSpan().IndexOf(",qop=auth,digest-uri="u8) >= 0);
        Assert.IsFalse(sent.AsSpan().IndexOf("auth-conf"u8) >= 0);
        Assert.IsFalse(sent.AsSpan().IndexOf("cipher="u8) >= 0);
        Assert.IsTrue(sent.AsSpan().IndexOf("dc=example"u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerOffersOnlyAuthConf_SendsNoAnswerAndFailsWith67AuthenticationCancelled()
    {
        (TransferResult result, byte[] sent) = await RunAsync(HttpAuthSchemes.Digest, null, MechanismsEntry(1), RootDseDone1, Challenge(2, "nonce=\"abc\",qop=\"auth-conf\",cipher=\"rc4\""));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled"), result);
        Assert.IsFalse(sent.AsSpan().IndexOf("response="u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestChallengeWithoutANonce_SendsNoAnswerAndFailsWith67AuthenticationCancelled()
    {
        (TransferResult result, byte[] sent) = await RunAsync(HttpAuthSchemes.Digest, null, MechanismsEntry(1), RootDseDone1, Challenge(2, "realm=\"x\",qop=\"auth\""));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled"), result);
        Assert.IsFalse(sent.AsSpan().IndexOf("response="u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestBindRefused_FailsWith67()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Digest, null, MechanismsEntry(1), RootDseDone1, "30 0c 02 01 02 61 07 0a 01 31 04 00 04 00");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm, null)]
    [DataRow(HttpAuthSchemes.Basic, "AUTH=NTLM")]
    public async Task ExecuteAsync_ServerOffersNtlm_FailsWith67LikeCurlsOpenLdapBuild(HttpAuthSchemes schemes, string? loginOptions)
    {
        // Measured with curl 8.18.0 (OpenLDAP 2.6.10, Linux) against a server whose supportedSASLMechanisms is NTLM:
        // "curl: (67) Login denied". Its OpenLDAP backend never does an NTLM bind.
        (TransferResult result, byte[] sent) = await RunAsync(schemes, loginOptions, MechanismsEntry(1, "NTLM"), RootDseDone1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.IsFalse(sent.AsSpan().IndexOf("secret"u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestBindSucceedsWithoutAChallenge_FailsWith67LoginDenied()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Digest, null, MechanismsEntry(1), RootDseDone1, "30 0c 02 01 02 61 07 0a 01 00 04 00 04 00");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesReadingTheMechanisms_FailsWith7()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Digest, null);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicAndNoAuthOption_SendsTheSimpleBind()
    {
        (TransferResult result, byte[] sent) = await RunAsync(HttpAuthSchemes.Basic, "other", "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00", "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00");

        Assert.AreEqual(TransferResult.Success(0), result, result.ToString());
        Assert.IsTrue(sent.AsSpan().IndexOf("secret"u8) >= 0);
    }

    private static string Challenge(int id, string challenge)
    {
        byte[] text = Encoding.ASCII.GetBytes(challenge);
        int response = 9 + text.Length;
        return $"30 {5 + response - 2 + 2:x2} 02 01 {id:x2} 61 {response:x2} 0a 01 0e 04 00 04 00 87 {text.Length:x2} {Convert.ToHexString(text)}";
    }

    /// <summary>A SearchResultEntry for the rootDSE with <c>supportedSASLMechanisms: DIGEST-MD5</c>.</summary>
    private static string MechanismsEntry(int id, string mechanism = "DIGEST-MD5")
    {
        byte[] name = "supportedSASLMechanisms"u8.ToArray();
        byte[] value = Encoding.ASCII.GetBytes(mechanism);
        byte[] set = [0x31, (byte)(value.Length + 2), 0x04, (byte)value.Length, .. value];
        byte[] partial = [0x04, (byte)name.Length, .. name, .. set];
        byte[] attribute = [0x30, (byte)partial.Length, .. partial];
        byte[] entry = [0x04, 0x00, 0x30, (byte)attribute.Length, .. attribute];
        byte[] op = [0x64, (byte)entry.Length, .. entry];
        byte[] message = [0x02, 0x01, (byte)id, .. op];
        return Convert.ToHexString([0x30, (byte)message.Length, .. message]);
    }

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(HttpAuthSchemes schemes, string? loginOptions, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.OpenLdap, new FakeLogonTokenSource(2));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18389/dc=example"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("cn=u,dc=x", "secret"),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = schemes },
            Mail = new MailRequestOptions { LoginOptions = loginOptions },
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent);
    }
}
