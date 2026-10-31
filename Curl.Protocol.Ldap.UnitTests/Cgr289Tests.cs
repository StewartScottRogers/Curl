using System.Net.Security;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins CGR-289 (BL-3818): curl 8.22.0's Schannel build opens WinLDAP's own <c>ldaps</c>
/// connection with the machine name as SNI and the system store as the only trust; the plaintext
/// connection and the connection curl itself opens are unchanged. Over <c>ldaps</c> WinLDAP's
/// NTLM bind asks for no signing or sealing (flags <c>07 b2 08 a2</c>), the session gets no
/// security layer, and a server that closes after the <c>sicilyNegotiate</c> fails with
/// <c>Unavailable</c> (measured by BL-4044, pinned by BL-4046). A server that answers the bind
/// gets one negotiate, one authenticate, the search and an unbind, as from real curl (BL-4055).
/// </summary>
[TestClass]
public sealed class Cgr289Tests
{
    private const string NtlmChallenge = "4e 54 4c 4d 53 53 50 00 02 00 00 00";

    private static string Done(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string SicilyChallenge(int id) => $"30 18 02 01 {id:x2} 61 13 0a 01 00 04 0c {NtlmChallenge} 04 00";

    private static string BindReply(int id) => $"30 0c 02 01 {id:x2} 61 07 0a 01 00 04 00 04 00";

    private static async Task<(TransferResult Result, List<ConnectTarget> Targets)> RunAsync(string url, FakeLogonTokenSource tokens, params string[] replies)
    {
        var connector = new RecordingTargets(new ScriptedConnection([.. replies.Select(Hex.Bytes)]));
        var handler = new LdapProtocolHandler(connector, LdapDialect.WinLdap, tokens);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        return (result, connector.Targets);
    }

    private static async Task<List<ConnectTarget>> TargetsOfAsync(string url) =>
        (await RunAsync(url, new FakeLogonTokenSource(2, "4e", "4e"))).Targets;

    [TestMethod]
    public async Task ExecuteAsync_LdapsWithoutCredentials_SecondConnectionSendsMachineNameAndTrustsSystemStore()
    {
        List<ConnectTarget> targets = await TargetsOfAsync("ldaps://127.0.0.1:21900/dc=test");

        Assert.HasCount(2, targets);
        Assert.IsNull(targets[0].TlsServerName);
        Assert.IsFalse(targets[0].TrustsSystemStoreOnly);
        Assert.AreEqual(Environment.MachineName, targets[1].TlsServerName);
        Assert.IsTrue(targets[1].TrustsSystemStoreOnly);
        Assert.AreEqual(targets[0].Host, targets[1].Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapWithoutCredentials_SecondConnectionKeepsTheTargetAsIs()
    {
        List<ConnectTarget> targets = await TargetsOfAsync("ldap://127.0.0.1:21900/dc=test");

        Assert.AreEqual(targets[0], targets[1]);
        Assert.IsNull(targets[1].TlsServerName);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapsSicilyBind_AsksForNoProtectionAndStartsNoSecurityLayer()
    {
        var tokens = new FakeLogonTokenSource(2, "4e", "4e");

        (TransferResult result, _) = await RunAsync("ldaps://127.0.0.1:21900/dc=test", tokens, Done(1), Done(2), Done(3), SicilyChallenge(4), BindReply(5), Done(6));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(new[] { ProtectionLevel.None }, tokens.Protections.ToArray());
        Assert.AreEqual(1, tokens.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapsSicilyBind_BindsOnceSearchesOnceAndUnbindsAsMessageSeven()
    {
        var connection = new ScriptedConnection([.. new[] { Done(1), Done(2), Done(3), SicilyChallenge(4), BindReply(5), Done(6) }.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingTargets(connection), LdapDialect.WinLdap, new FakeLogonTokenSource(2, "4e", "4e"));

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ldaps://127.0.0.1:21900/dc=test"), Output = new MemoryStream() });

        byte[] sent = connection.Sent;
        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(2, CountOf(sent, [0x60, 0x84]), "the sicilyNegotiate and the one authenticate bind");
        Assert.AreEqual(1, CountOf(sent, "dc=test"u8.ToArray()));
        CollectionAssert.AreEqual(Hex.Bytes("30 84 00 00 00 05 02 01 07 42 00"), sent[^11..]);
    }

    private static int CountOf(byte[] haystack, byte[] needle)
    {
        int count = 0;
        ReadOnlySpan<byte> rest = haystack;
        for (int index = rest.IndexOf(needle); index >= 0; index = rest.IndexOf(needle))
        {
            count++;
            rest = rest[(index + needle.Length)..];
        }

        return count;
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapSicilyBind_AsksForSigningAndSealing()
    {
        var tokens = new FakeLogonTokenSource(2, "4e", "4e");

        await RunAsync("ldap://127.0.0.1:21900/dc=test", tokens, Done(1), Done(2), Done(3), SicilyChallenge(4), BindReply(5));

        CollectionAssert.AreEqual(new[] { ProtectionLevel.EncryptAndSign }, tokens.Protections.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapsServerClosesAfterSicilyNegotiate_FailsWithUnavailable()
    {
        (TransferResult result, _) = await RunAsync("ldaps://127.0.0.1:21900/dc=test", new FakeLogonTokenSource(2, "4e", "4e"), Done(1), Done(2), Done(3));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Unavailable"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapServerClosesAfterSicilyNegotiate_FailsWithTimeout()
    {
        (TransferResult result, _) = await RunAsync("ldap://127.0.0.1:21900/dc=test", new FakeLogonTokenSource(2, "4e", "4e"), Done(1), Done(2), Done(3));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapsWinLdapHandshakeFails_FailsWithServerDown()
    {
        TransferResult result = await RunWithFailingSecondConnectAsync("ldaps://127.0.0.1:21900/dc=test");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Server Down"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LdapWinLdapConnectFails_FailsWithTheConnectFailure()
    {
        TransferResult result = await RunWithFailingSecondConnectAsync("ldap://127.0.0.1:21900/dc=test");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("refused", result.ErrorMessage);
    }

    private static async Task<TransferResult> RunWithFailingSecondConnectAsync(string url)
    {
        var handler = new LdapProtocolHandler(new SecondConnectFails(new ScriptedConnection([])), LdapDialect.WinLdap, new FakeLogonTokenSource(2, "4e", "4e"));

        return await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });
    }

    private sealed class SecondConnectFails(ScriptedConnection connection) : IConnector
    {
        private int connects;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(++connects == 1 ? ConnectResult.Connected(connection) : ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));
    }

    private sealed class RecordingTargets(ScriptedConnection connection) : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }
}
