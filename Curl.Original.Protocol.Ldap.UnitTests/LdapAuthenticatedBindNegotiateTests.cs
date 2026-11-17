using System.Net;
using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins <c>-u --negotiate</c> (BL-5314): the rootDSE read, then a SASL <c>GSS-SPNEGO</c> bind
/// (or <c>DIGEST-MD5</c> when the server does not offer it), never a Sicily bind; <c>--ntlm</c>
/// still sends the Sicily bind asking to sign and seal.
/// </summary>
[TestClass]
public sealed class LdapAuthenticatedBindNegotiateTests
{
    private const string NtlmChallenge = "4e 54 4c 4d 53 53 50 00 02 00 00 00";

    private static readonly byte[] SicilyNegotiateTag = [0x04, 0x04, (byte)'N', (byte)'T', (byte)'L', (byte)'M', 0x8a];

    [TestMethod]
    public async Task ExecuteAsync_NegotiateWithCredentialsAndSpnegoOffered_ReadsTheRootDseThenBindsWithGssSpnegoAndNoSicily()
    {
        var tokens = new FakeLogonTokenSource(2, "4e 54 4c 4d", "4e 54 4c 4e");

        (TransferResult result, byte[] sent) = await RunAsync(
            HttpAuthSchemes.Negotiate,
            tokens,
            CapabilitiesEntry(1),
            Done(1),
            MechanismsEntry(2),
            Done(2),
            $"30 1a 02 01 03 61 15 0a 01 0e 04 00 04 00 87 0c {NtlmChallenge}",
            BindReply(4),
            Sealed(0, Done(5)));

        Assert.AreEqual(TransferResult.Success(0), result);
        int capabilities = IndexOf(sent, "supportedCapabilities"u8);
        int mechanisms = IndexOf(sent, "supportedSASLMechanisms"u8);
        int bind = IndexOf(sent, "GSS-SPNEGO"u8);
        Assert.IsTrue(capabilities > 0 && capabilities < mechanisms && mechanisms < bind);
        Assert.AreEqual(2, CountOf(sent, "GSS-SPNEGO"u8.ToArray()));
        Assert.AreEqual(0, CountOf(sent, SicilyNegotiateTag));
        Assert.AreEqual(0, CountOf(sent, "NTLMSSP\0"u8.ToArray()));
        CollectionAssert.AreEqual(new[] { (LdapLogonPackage.Negotiate, "ldap/127.0.0.1") }, tokens.Starts.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateWithCredentialsAndOnlyDigestMd5Offered_BindsWithDigestMd5()
    {
        var tokens = new FakeLogonTokenSource(1);

        (TransferResult result, byte[] sent) = await RunAsync(
            HttpAuthSchemes.Negotiate,
            tokens,
            Done(1),
            DigestOnlyEntry(2),
            Done(2),
            Done(3),
            Challenge(4, "nonce=\"abc\",qop=\"auth\""),
            BindReply(5),
            Done(6));

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(2, CountOf(sent, "DIGEST-MD5"u8.ToArray()));
        Assert.AreEqual(0, CountOf(sent, "GSS-SPNEGO"u8.ToArray()));
        Assert.AreEqual(0, CountOf(sent, SicilyNegotiateTag));
        Assert.AreEqual(0, tokens.Starts.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateOverLdaps_AsksForNoProtectionAndSendsTheSearchUnsealed()
    {
        var tokens = new FakeLogonTokenSource(2, "4e 54 4c 4d", "4e 54 4c 4e");

        (TransferResult result, _) = await RunAsync(
            "ldaps",
            HttpAuthSchemes.Negotiate,
            tokens,
            Done(1),
            MechanismsEntry(2),
            Done(2),
            $"30 1a 02 01 03 61 15 0a 01 0e 04 00 04 00 87 0c {NtlmChallenge}",
            BindReply(4),
            Done(5));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(new[] { System.Net.Security.ProtectionLevel.None }, tokens.Protections.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateServerClosesDuringTheRootDseRead_FailsWith38Timeout()
    {
        (TransferResult result, _) = await RunAsync(HttpAuthSchemes.Negotiate, new FakeLogonTokenSource(1));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiatePackageProducesNoToken_FailsWith38LocalError()
    {
        (TransferResult result, _) = await RunAsync(
            HttpAuthSchemes.Negotiate,
            new FakeLogonTokenSource(1, (string?)null),
            Done(1),
            MechanismsEntry(2),
            Done(2));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmWithCredentials_StillSendsTheSicilyBindAskingToSignAndSeal()
    {
        (TransferResult result, byte[] sent) = await RunAsync(HttpAuthSchemes.Ntlm, new FakeLogonTokenSource(1), BindReply(1));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Local Error"), result);
        Assert.AreEqual(1, CountOf(sent, SicilyNegotiateTag));
        int message = IndexOf(sent, "NTLMSSP\0"u8);
        Assert.AreEqual(0x30, sent[message + 12] & 0x30);
        Assert.AreEqual(0, CountOf(sent, "supportedSASLMechanisms"u8.ToArray()));
    }

    [TestMethod]
    public void ForCredential_NegotiateLogonTokenSource_ReturnsAnotherSourceForTheCredential()
    {
        ILdapLogonTokenSource source = new NegotiateLogonTokenSource();

        ILdapLogonTokenSource bound = source.ForCredential(new NetworkCredential("u", "p"));

        Assert.IsNotNull(bound);
        Assert.AreNotSame(source, bound);
    }

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(HttpAuthSchemes schemes, FakeLogonTokenSource tokens, params string[] replies) =>
        await RunAsync("ldap", schemes, tokens, replies);

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(string scheme, HttpAuthSchemes schemes, FakeLogonTokenSource tokens, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, tokens);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse($"{scheme}://127.0.0.1:18389/dc=example"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("admin", "pw"),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = schemes },
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent);
    }

    private static string BindReply(int id) => $"30 0c 02 01 {id:x2} 61 07 0a 01 00 04 00 04 00";

    private static string Done(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string Sealed(int sequenceNumber, string message)
    {
        int length = 16 + Hex.Bytes(message).Length;
        return $"00 00 {length >> 8:x2} {length & 0xff:x2} {FakeLogonTokenSource.Signature(sequenceNumber)} {message}";
    }

    private static string Challenge(int id, string challenge)
    {
        byte[] text = Encoding.ASCII.GetBytes(challenge);
        int response = 9 + text.Length;
        return $"30 {5 + response - 2 + 2:x2} 02 01 {id:x2} 61 {response:x2} 0a 01 0e 04 00 04 00 87 {text.Length:x2} {Convert.ToHexString(text)}";
    }

    private static string CapabilitiesEntry(int id) =>
        $"30 3c 02 01 {id:x2} 64 37 04 00 30 33 30 31 04 15 73 75 70 70 6f 72 74 65 64 43 61 70 61 62 69 6c 69 74 69 65 73 31 18 04 16 31 2e 32 2e 38 34 30 2e 31 31 33 35 35 36 2e 31 2e 34 2e 38 30 30";

    /// <summary>A rootDSE entry whose <c>supportedSASLMechanisms</c> are <c>GSS-SPNEGO</c> and <c>GSSAPI</c>.</summary>
    private static string MechanismsEntry(int id) =>
        $"30 3a 02 01 {id:x2} 64 35 04 00 30 31 30 2f 04 17 73 75 70 70 6f 72 74 65 64 53 41 53 4c 4d 65 63 68 61 6e 69 73 6d 73 31 14 04 0a 47 53 53 2d 53 50 4e 45 47 4f 04 06 47 53 53 41 50 49";

    /// <summary>A rootDSE entry whose only <c>supportedSASLMechanisms</c> value is <c>DIGEST-MD5</c>.</summary>
    private static string DigestOnlyEntry(int id)
    {
        string name = Convert.ToHexString("supportedSASLMechanisms"u8);
        string value = Convert.ToHexString("DIGEST-MD5"u8);
        return $"30 32 02 01 {id:x2} 64 2d 04 00 30 29 30 27 04 17 {name} 31 0c 04 0a {value}";
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle);

    private static int CountOf(byte[] haystack, byte[] needle)
    {
        int count = 0;
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            count += haystack.AsSpan(i, needle.Length).SequenceEqual(needle) ? 1 : 0;
        }

        return count;
    }
}
