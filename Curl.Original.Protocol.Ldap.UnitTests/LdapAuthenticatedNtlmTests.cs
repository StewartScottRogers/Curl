using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins the Sicily NTLM challenge answer of the LDAP <c>--ntlm</c> and <c>--negotiate</c> bind (BL-4990).</summary>
[TestClass]
public sealed class LdapAuthenticatedNtlmTests
{
    private const string BindSuccess2 = "30 0c 02 01 02 61 07 0a 01 00 04 00 04 00";

    private const string BindInvalidCredentials2 = "30 0c 02 01 02 61 07 0a 01 31 04 00 04 00";

    private const string SearchDone3 = "30 0c 02 01 03 65 07 0a 01 00 04 00 04 00";

    /// <summary>A NTLMSSP challenge (type 2) with no target name, no extended session security and server challenge 01..08.</summary>
    private const string ChallengeMessage = "4e544c4d53535000 02000000 0000 0000 20000000 00000000 0102030405060708 0000000000000000";

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthSchemes.Negotiate)]
    public async Task ExecuteAsync_ChallengeAnswered_SendsTheSicilyResponseWithTag0x8bAndNoPassword(HttpAuthSchemes schemes)
    {
        (TransferResult result, byte[] sent) = await RunAsync(schemes, ChallengeReply(), BindSuccess2, SearchDone3);

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(2, CountOf(sent, "NTLMSSP\0"u8.ToArray()));
        Assert.IsTrue(Contains(sent, [0x04, 0x00, 0x8b]));
        Assert.IsFalse(Contains(sent, "secret"u8.ToArray()));
        Assert.IsTrue(Contains(sent, [3, 0, 0, 0]));
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseRefused_FailsWith38WithTheServersResult()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Ntlm, ChallengeReply(), BindInvalidCredentials2);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChallengeIsNotNtlm_FailsWith38LocalError()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Ntlm, ChallengeReply("0102030405060708090a0b0c0d0e0f1011121314151617181920212223242526"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserNameTooLongForTheMessage_FailsWith38LocalError()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Ntlm, "cn=" + new string('u', 2000), [ChallengeReply()]);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
    }

    private static string ChallengeReply(string? message = null)
    {
        byte[] challenge = Hex.Bytes(message ?? ChallengeMessage);
        return $"30 {(challenge.Length + 12):x2} 02 01 01 61 {(challenge.Length + 7):x2} 0a 01 00 04 {challenge.Length:x2} {Convert.ToHexString(challenge)} 04 00";
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

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(HttpAuthSchemes schemes, params string[] replies) => await RunAsync(schemes, "cn=u,dc=x", replies);

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(HttpAuthSchemes schemes, string userName, string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, new FakeLogonTokenSource(2));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18389/dc=example"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(userName, "secret"),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = schemes },
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent);
    }
}
