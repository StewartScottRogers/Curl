using System.Net;
using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    private const string PlainBindSuccess = "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00";

    private const string BindSuccess2 = "30 0c 02 01 02 61 07 0a 01 00 04 00 04 00";

    private const string BindInvalidCredentials = "30 0c 02 01 01 61 07 0a 01 31 04 00 04 00";

    private const string SearchEntry = "30 0d 02 01 02 64 08 04 04 63 6e 3d 61 30 00";

    private const string SearchDone3 = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    private const string DigestSearchEntry = "30 0d 02 01 03 64 08 04 04 63 6e 3d 61 30 00";

    private const string DigestSearchDone = "30 0c 02 01 03 65 07 0a 01 00 04 00 04 00";

    [TestMethod]
    public async Task ExecuteAsync_DigestAnsweredWithPlainSuccess_FailsWith38ProtocolErrorAndSendsNoSearch()
    {
        (TransferResult result, byte[] sent, string output) = await RunAsync(HttpAuthSchemes.Digest, PlainBindSuccess, SearchEntry, SearchDone3);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Protocol Error"), result);
        Assert.IsFalse(sent.Contains((byte)0x63), "No SearchRequest (tag 0x63) may be sent.");
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestRefusedByTheServer_FailsWith38WithTheServersResult()
    {
        (TransferResult result, _, _) = await RunAsync(HttpAuthSchemes.Digest, BindInvalidCredentials);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestChallengeAnswered_BindsAndReturnsTheEntry()
    {
        (TransferResult result, byte[] sent, string output) = await RunAsync(HttpAuthSchemes.Digest, ChallengeReply(), BindSuccess2, DigestSearchEntry, DigestSearchDone);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(Encoding.ASCII.GetString(sent).Contains("digest-uri", StringComparison.Ordinal));
        StringAssert.Contains(output, "cn=a");
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicAgainstAlwaysSuccessServer_BindsAndReturnsTheEntry()
    {
        (TransferResult result, _, string output) = await RunAsync(HttpAuthSchemes.Basic, PlainBindSuccess, SearchEntry, SearchDone3);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(output, "cn=a");
    }

    private static string ChallengeReply()
    {
        byte[] creds = Encoding.ASCII.GetBytes("realm=\"r\",nonce=\"abc\",qop=\"auth\",charset=utf-8,algorithm=md5-sess");
        return $"30 {(creds.Length + 14):x2} 02 01 01 61 {(creds.Length + 9):x2} 0a 01 0e 04 00 04 00 87 {creds.Length:x2} {Convert.ToHexString(creds)}";
    }

    private static async Task<(TransferResult Result, byte[] Sent, string Output)> RunAsync(HttpAuthSchemes schemes, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, new FakeLogonTokenSource(2));
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18389/dc=example"),
            Output = output,
            Credentials = new NetworkCredential("cn=admin,dc=x", "pw"),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = schemes },
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent, Encoding.UTF8.GetString(output.ToArray()));
    }
}