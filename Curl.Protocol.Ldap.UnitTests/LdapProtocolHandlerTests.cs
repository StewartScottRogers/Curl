using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the <c>ldap://</c> bind against the reference builds, recorded with
/// <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-28 (BL-586): curl 8.21.0 with WinLDAP on
/// Windows for <see cref="LdapDialect.WinLdap" />, curl 8.18.0 with OpenLDAP 2.6.10 on Linux
/// for <see cref="LdapDialect.OpenLdap" />; the bytes each sent, the exit code and message.
/// Both dialects are pinned on every operating system, since the dialect is a constructor
/// argument.
/// </summary>
[TestClass]
public sealed partial class LdapProtocolHandlerTests
{
    private const string WinLdapBindV3 = "30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74";

    private const string WinLdapBindV2 = "30 84 00 00 00 1f 02 01 02 60 84 00 00 00 16 02 01 02 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74";

    private const string WinLdapUnbind2 = "30 84 00 00 00 05 02 01 02 42 00";

    private const string WinLdapUnbind3 = "30 84 00 00 00 05 02 01 03 42 00";

    private const string OpenLdapBind = "30 1b 02 01 01 60 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74";

    private const string OpenLdapAnonymousBind = "30 0c 02 01 01 60 07 02 01 03 04 00 80 00";

    private const string OpenLdapUnbind = "30 05 02 01 02 42 00";

    private const string WinLdapUnbind4 = "30 84 00 00 00 05 02 01 04 42 00";

    private const string OpenLdapUnbind3 = "30 05 02 01 03 42 00";

    /// <summary>The SearchRequest for <c>dc=example</c>, messageID 2, recorded from each build (BL-587).</summary>
    private const string WinLdapSearch2 = "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00";

    private const string WinLdapSearch3 = "30 84 00 00 00 37 02 01 03 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00";

    private const string OpenLdapSearch2 = "30 2f 02 01 02 63 2a 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00";

    private const string BindSuccess1 = "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00";

    private const string SearchDone2 = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    private const string BindInvalidCredentials1 = "30 0c 02 01 01 61 07 0a 01 31 04 00 04 00";

    private const string NotABindResponse = "30 05 02 01 01 04 00";

    private const string NotASequence = "04 00";

    [TestMethod]
    public void SupportedSchemes_AreLdapAndLdaps()
    {
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())), LdapDialect.OpenLdap);

        CollectionAssert.AreEqual(new[] { "ldap", "ldaps" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LdapProtocolHandler(null!, LdapDialect.OpenLdap));
    }

    [TestMethod]
    public void Constructor_UndefinedDialect_Throws()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LdapProtocolHandler(connector, (LdapDialect)2));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())), LdapDialect.OpenLdap);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    [DataRow("ldap://h/dc=example", 389, false)]
    [DataRow("ldaps://h/dc=example", 636, true)]
    [DataRow("ldap://h:1389/dc=example", 1389, false)]
    [DataRow("ldaps://h:1636/dc=example", 1636, true)]
    public async Task ExecuteAsync_Url_ConnectsToItsPortWithTlsForLdaps(string url, int port, bool useTls)
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Hex.Bytes(BindSuccess1))));

        await new LdapProtocolHandler(connector, LdapDialect.OpenLdap).ExecuteAsync(Context(url, null));

        Assert.AreEqual(new ConnectTarget("h", port, useTls), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect to h port 389"));

        TransferResult result = await new LdapProtocolHandler(connector, LdapDialect.OpenLdap).ExecuteAsync(Context("ldap://h/", null));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to h port 389", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_Bound_DisposesTheConnection()
    {
        var connection = new ScriptedConnection(Hex.Bytes(BindSuccess1));

        await new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.OpenLdap).ExecuteAsync(Context("ldap://h/", null));

        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapBound_SendsTheBindTheSearchThenTheUnbindAndSucceeds()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, User, BindSuccess1, SearchDone2);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindV3 + " " + WinLdapSearch2 + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapBindRefusedTwice_RetriesAsLdapV2ThenUnbindsAndFailsWith38()
    {
        (TransferResult result, byte[] sent) = await RunAsync(
            LdapDialect.WinLdap,
            User,
            BindInvalidCredentials1,
            "30 0c 02 01 02 61 07 0a 01 31 04 00 04 00");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindV3 + " " + WinLdapBindV2 + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow("35", "LDAP local: bind via ldap_win_bind Unwilling To Perform")]
    [DataRow("64", "LDAP local: bind via ldap_win_bind ")]
    public async Task ExecuteAsync_WinLdapRetryRefused_FailsWithWinLdapsTextForTheRetrysResult(string resultCode, string message)
    {
        (TransferResult result, _) = await RunAsync(
            LdapDialect.WinLdap,
            User,
            $"30 0c 02 01 01 61 07 0a 01 {resultCode} 04 00 04 00",
            $"30 0c 02 01 02 61 07 0a 01 {resultCode} 04 00 04 00");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, message), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapRetrySucceeds_SearchesWithMessageId3AndUnbindsWith4()
    {
        (TransferResult result, byte[] sent) = await RunAsync(
            LdapDialect.WinLdap,
            User,
            BindInvalidCredentials1,
            "30 0c 02 01 02 61 07 0a 01 00 04 00 04 00",
            "30 0c 02 01 03 65 07 0a 01 00 04 00 04 00");

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindV3 + " " + WinLdapBindV2 + " " + WinLdapSearch3 + " " + WinLdapUnbind4), sent);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(NotABindResponse)]
    [DataRow(NotASequence)]
    public async Task ExecuteAsync_WinLdapFirstBindUnanswered_FailsWith38TimeoutWithoutRetrying(string? reply)
    {
        (TransferResult result, byte[] sent) = reply is null
            ? await RunAsync(LdapDialect.WinLdap, User)
            : await RunAsync(LdapDialect.WinLdap, User, reply);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindV3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapServerClosesOnTheRetry_FailsWith38Unavailable()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, User, BindInvalidCredentials1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Unavailable"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindV3 + " " + WinLdapBindV2), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapRetryAnsweredWithSomethingElse_FailsWith38Timeout()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, User, BindInvalidCredentials1, "30 05 02 01 02 04 00");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Timeout"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindV3 + " " + WinLdapBindV2), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapBound_SendsTheBindTheSearchThenTheUnbindAndSucceeds()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.OpenLdap, User, BindSuccess1, SearchDone2);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBind + " " + OpenLdapSearch2 + " " + OpenLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithoutUser_BindsAnonymously()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.OpenLdap, null, BindSuccess1, SearchDone2);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapAnonymousBind + " " + OpenLdapSearch2 + " " + OpenLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithoutUser_BindsAnonymouslyUntilTheNtlmBindIsBuilt()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.WinLdap, null, BindSuccess1, SearchDone2);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes("30 84 00 00 00 10 02 01 01 60 84 00 00 00 07 02 01 03 04 00 80 00 " + WinLdapSearch2 + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExecuteAsync_OpenLdapInvalidCredentials_UnbindsAndFailsWith67LoginDenied(bool withUser)
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.OpenLdap, withUser ? User : null, BindInvalidCredentials1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        CollectionAssert.AreEqual(Hex.Bytes((withUser ? OpenLdapBind : OpenLdapAnonymousBind) + " " + OpenLdapUnbind), sent);
    }

    [TestMethod]
    [DataRow("30 0c 02 01 01 61 07 0a 01 35 04 00 04 00")]
    [DataRow(NotABindResponse)]
    [DataRow(NotASequence)]
    public async Task ExecuteAsync_OpenLdapOtherBindFailure_UnbindsAndFailsWith38CannotBind(string reply)
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.OpenLdap, User, reply);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP: cannot bind"), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBind + " " + OpenLdapUnbind), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapServerClosesBeforeTheBindResponse_FailsWith7()
    {
        (TransferResult result, byte[] sent) = await RunAsync(LdapDialect.OpenLdap, User);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, "LDAP local: connecting ldap_result Can't contact LDAP server"), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBind), sent);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_BindResponseOneByteAtATime_IsReadWhole(LdapDialect dialect)
    {
        byte[][] reads = [.. Hex.Bytes(BindSuccess1 + " " + SearchDone2).Select(octet => new[] { octet })];
        var connection = new ScriptedConnection(reads);

        TransferResult result = await new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect)
            .ExecuteAsync(Context("ldap://h/dc=example", User));

        Assert.AreEqual(TransferResult.Success(0), result);
    }

    private static NetworkCredential User => new("cn=u,dc=x", "secret");

    private static async Task<(TransferResult Result, byte[] Sent)> RunAsync(LdapDialect dialect, NetworkCredential? credentials, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect);

        TransferResult result = await handler.ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", credentials));

        return (result, connection.Sent);
    }

    private static TransferContext Context(string url, NetworkCredential? credentials) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Credentials = credentials,
    };
}
