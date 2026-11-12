using System.Net;
using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the WinLDAP bind the handler makes for each auth mask (ADR-1246): <c>--ntlm</c>,
/// <c>--negotiate</c> and <c>--digest</c> never send a simple bind with the password.
/// </summary>
[TestClass]
public sealed class LdapAuthSchemeBindTests
{
    private const string BindSuccess1 = "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00";

    private const string BindInvalidCredentials1 = "30 0c 02 01 01 61 07 0a 01 31 04 00 04 00";

    private const string SearchDone2 = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    private const string SearchDone3 = "30 0c 02 01 03 65 07 0a 01 00 04 00 04 00";

    private static NetworkCredential User => new("cn=u,dc=x", "secret");

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthSchemes.Negotiate)]
    public async Task ExecuteAsync_NtlmOrNegotiateRefused_SendsTheSicilyNegotiateAndNoPasswordThenFailsWith38(HttpAuthSchemes schemes)
    {
        (TransferResult result, byte[] sent) = await RunAsync(schemes, BindInvalidCredentials1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
        Assert.IsTrue(sent.AsSpan().IndexOf("NTLMSSP\0"u8) > 0);
        Assert.IsTrue(Contains(sent, [0x04, 0x04, (byte)'N', (byte)'T', (byte)'L', (byte)'M', 0x8a]));
        Assert.IsFalse(Contains(sent, "secret"u8.ToArray()));
        Assert.IsTrue(Contains(sent, Hex.Bytes("30 84 00 00 00 05 02 01 02 42 00")));
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmServerSendsAChallenge_FailsWith38LocalError()
    {
        (TransferResult result, byte[] sent) = await RunAsync(HttpAuthSchemes.Ntlm, BindSuccess1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        Assert.IsFalse(Contains(sent, "secret"u8.ToArray()));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthSchemes.Digest)]
    public async Task ExecuteAsync_ServerClosesDuringTheBind_FailsWith38Timeout(HttpAuthSchemes schemes)
    {
        (TransferResult result, _) = await RunAsync(schemes);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestAnswersTheChallenge_SendsTag0xa3TwiceAndNoPassword()
    {
        (TransferResult result, byte[] sent) = await RunAsync(HttpAuthSchemes.Digest, Challenge(1, "nonce=\"abc\",qop=\"auth\""), BindSuccess(2), SearchDone3);

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(2, CountOf(sent, Encoding.ASCII.GetBytes("DIGEST-MD5")));
        Assert.IsTrue(Contains(sent, "response="u8.ToArray()));
        Assert.IsFalse(Contains(sent, "secret"u8.ToArray()));
        Assert.IsFalse(Contains(sent, [0x80, 0x06]));
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestChallengeWithoutANonce_FailsWith38LocalError()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Digest, Challenge(1, "realm=\"x\""));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestInitialBindRefused_FailsWith38()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Digest, BindInvalidCredentials1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic)]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Ntlm | HttpAuthSchemes.Digest)]
    public async Task ExecuteAsync_BasicInTheMask_SendsTheSimpleBindWithThePassword(HttpAuthSchemes schemes)
    {
        (TransferResult result, byte[] sent) = await RunAsync(schemes, BindSuccess1, SearchDone2);

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.IsTrue(Contains(sent, [0x80, 0x06, .. "secret"u8.ToArray()]));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptions_SendsTheSimpleBind()
    {
        (TransferResult result, byte[] sent) = await RunAsync(null, BindSuccess1, SearchDone2);

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.IsTrue(Contains(sent, [0x80, 0x06, .. "secret"u8.ToArray()]));
    }

    private static string BindSuccess(int id) => $"30 0c 02 01 {id:x2} 61 07 0a 01 00 04 00 04 00";

    /// <summary>A BindResponse <c>saslBindInProgress</c> whose server credentials are <paramref name="challenge" />.</summary>
    private static string Challenge(int id, string challenge)
    {
        byte[] text = Encoding.ASCII.GetBytes(challenge);
        int response = 9 + text.Length;
        return $"30 {5 + response - 2 + 2:x2} 02 01 {id:x2} 61 {response:x2} 0a 01 0e 04 00 04 00 87 {text.Length:x2} {Convert.ToHexString(text)}";
    }

    private static bool Contains(byte[] haystack, byte[] needle) => CountOf(haystack, needle) > 0;

    private static int CountOf(byte[] haystack, byte[] needle)
    {
        int count = 0;
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            count += haystack.AsSpan(i, needle.Length).SequenceEqual(needle) ? 1 : 0;
        }

        return count;
    }

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(HttpAuthSchemes? schemes, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, new FakeLogonTokenSource(2));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18389/dc=example"),
            Output = new MemoryStream(),
            Credentials = User,
            Http = schemes is { } mask ? new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = mask } : null,
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent);
    }
}
