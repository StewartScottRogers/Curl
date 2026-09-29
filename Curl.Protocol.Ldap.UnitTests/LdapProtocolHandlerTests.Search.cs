using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <content>
/// Pins the search an <c>ldap://</c> URL names against the reference builds, recorded with
/// <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-28 (BL-587) with <c>-sS -u cn=u:p</c>:
/// curl 8.21.0 with WinLDAP on Windows for <see cref="LdapDialect.WinLdap" />, curl 8.18.0
/// with OpenLDAP 2.6.10 on Linux for <see cref="LdapDialect.OpenLdap" />; every byte each sent
/// after its BindRequest, the exit code and the message.
/// </content>
public sealed partial class LdapProtocolHandlerTests
{
    private const string WinLdapBindCnU = "30 84 00 00 00 15 02 01 01 60 84 00 00 00 0c 02 01 03 04 04 63 6e 3d 75 80 01 70";

    private const string OpenLdapBindCnU = "30 11 02 01 01 60 0c 02 01 03 04 04 63 6e 3d 75 80 01 70";

    private const string WinLdapSearchX = "30 84 00 00 00 2e 02 01 02 63 84 00 00 00 25 04 01 78 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00";

    private const string OpenLdapSearchX = "30 26 02 01 02 63 21 04 01 78 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00";

    private const string SearchSuccess = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    private const string SearchNoSuchObject = "30 0c 02 01 02 65 07 0a 01 20 04 00 04 00";

    [TestMethod]
    [DataRow("dc=example", "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00")]
    [DataRow("dc=example?cn,mail?sub?(uid=a)", "30 84 00 00 00 42 02 01 02 63 84 00 00 00 39 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 02 0a 01 00 02 01 00 02 01 00 01 01 00 a3 84 00 00 00 08 04 03 75 69 64 04 01 61 30 84 00 00 00 0a 04 02 63 6e 04 04 6d 61 69 6c")]
    [DataRow(@"dc=example??one?(&(|(uid=a*b)(cn=x\2a))(!(sn=*)))", "30 84 00 00 00 65 02 01 02 63 84 00 00 00 5c 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 01 0a 01 00 02 01 00 02 01 00 01 01 00 a0 84 00 00 00 35 a1 84 00 00 00 25 a4 84 00 00 00 11 04 03 75 69 64 30 84 00 00 00 06 80 01 61 82 01 62 a3 84 00 00 00 08 04 02 63 6e 04 02 78 2a a2 84 00 00 00 04 87 02 73 6e 30 84 00 00 00 00")]
    [DataRow("dc=example??SUB", "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 02 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00")]
    [DataRow("dc=example????!bogusext", "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00")]
    [DataRow("dc=example?a?base?(cn=a)?x?y", "30 84 00 00 00 3a 02 01 02 63 84 00 00 00 31 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 84 00 00 00 07 04 02 63 6e 04 01 61 30 84 00 00 00 03 04 01 61")]
    [DataRow("dc=example%zz", "30 84 00 00 00 3a 02 01 02 63 84 00 00 00 31 04 0d 64 63 3d 65 78 61 6d 70 6c 65 25 7a 7a 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00")]
    [DataRow("dc%3dexample?c%6e?base?(cn=a%20b)", "30 84 00 00 00 3d 02 01 02 63 84 00 00 00 34 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 84 00 00 00 09 04 02 63 6e 04 03 61 20 62 30 84 00 00 00 04 04 02 63 6e")]
    [DataRow("dc=example???cn=a", "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 84 00 00 00 07 04 02 63 6e 04 01 61 30 84 00 00 00 00")]
    [DataRow("dc=example???(cn=a)(cn=b)", "30 84 00 00 00 44 02 01 02 63 84 00 00 00 3b 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 84 00 00 00 07 04 02 63 6e 04 01 61 a3 84 00 00 00 07 04 02 63 6e 04 01 62 30 84 00 00 00 00")]
    [DataRow("???(|(cn>=a)(cn<=b)(cn~=c)(cn=*a*b*))", "30 84 00 00 00 63 02 01 02 63 84 00 00 00 5a 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a1 84 00 00 00 3d a5 84 00 00 00 07 04 02 63 6e 04 01 61 a6 84 00 00 00 07 04 02 63 6e 04 01 62 a8 84 00 00 00 07 04 02 63 6e 04 01 63 a4 84 00 00 00 10 04 02 63 6e 30 84 00 00 00 06 81 01 61 81 01 62 30 84 00 00 00 00")]
    [DataRow("", "30 84 00 00 00 2d 02 01 02 63 84 00 00 00 24 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00")]
    [DataRow("x???(cn=%C3%A9)", "30 84 00 00 00 31 02 01 02 63 84 00 00 00 28 04 01 78 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 84 00 00 00 0a 04 02 63 6e 04 04 c3 83 c2 a9 30 84 00 00 00 00")]
    [DataRow("%80%9f?%80", "30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 05 e2 82 ac c5 b8 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 05 04 03 e2 82 ac")]
    [DataRow("x???(cn:dn:=a)", "30 84 00 00 00 31 02 01 02 63 84 00 00 00 28 04 01 78 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a9 84 00 00 00 0a 82 02 63 6e 83 01 61 84 01 ff 30 84 00 00 00 00")]
    public async Task ExecuteAsync_WinLdapUrl_SendsTheSearchWinLdapSent(string path, string search)
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.WinLdap, path, BindSuccess1, SearchSuccess);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + search + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow("dc=example", "30 2f 02 01 02 63 2a 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00")]
    [DataRow("dc=example?cn,mail?sub?(uid=a)", "30 36 02 01 02 63 31 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 02 0a 01 00 02 01 00 02 01 00 01 01 00 a3 08 04 03 75 69 64 04 01 61 30 0a 04 02 63 6e 04 04 6d 61 69 6c")]
    [DataRow(@"dc=example??one?(&(|(uid=a*b)(cn=x\2a))(!(sn=*)))", "30 45 02 01 02 63 40 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 01 0a 01 00 02 01 00 02 01 00 01 01 00 a0 21 a1 19 a4 0d 04 03 75 69 64 30 06 80 01 61 82 01 62 a3 08 04 02 63 6e 04 02 78 2a a2 04 87 02 73 6e 30 00")]
    [DataRow("dc=example??SUB", "30 2f 02 01 02 63 2a 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 02 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00")]
    [DataRow("dc=example????!bogusext", "30 2f 02 01 02 63 2a 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00")]
    [DataRow("dc=example%zz", "30 25 02 01 02 63 20 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00")]
    [DataRow("dc%3dexample?c%6e?base?(cn=a%20b)", "30 31 02 01 02 63 2c 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 09 04 02 63 6e 04 03 61 20 62 30 04 04 02 63 6e")]
    [DataRow("dc=example???cn=a", "30 2b 02 01 02 63 26 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 07 04 02 63 6e 04 01 61 30 00")]
    [DataRow("???(|(cn>=a)(cn<=b)(cn~=c)(cn=*a*b*))", "30 43 02 01 02 63 3e 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a1 29 a5 07 04 02 63 6e 04 01 61 a6 07 04 02 63 6e 04 01 62 a8 07 04 02 63 6e 04 01 63 a4 0c 04 02 63 6e 30 06 81 01 61 81 01 62 30 00")]
    [DataRow("", "30 25 02 01 02 63 20 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00")]
    [DataRow("x???(cn=%C3%A9)", "30 23 02 01 02 63 1e 04 01 78 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a3 08 04 02 63 6e 04 02 c3 a9 30 00")]
    [DataRow("x%00y", OpenLdapSearchX)]
    [DataRow("x??subordinate", "30 26 02 01 02 63 21 04 01 78 0a 01 03 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 00")]
    [DataRow("x???(cn:dn:=a)", "30 25 02 01 02 63 20 04 01 78 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 a9 0a 82 02 63 6e 83 01 61 84 01 ff 30 00")]
    public async Task ExecuteAsync_OpenLdapUrl_SendsTheSearchOpenLdapSent(string path, string search)
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.OpenLdap, path, BindSuccess1, SearchSuccess);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + search + " " + OpenLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow("dc=example??bogus", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x??subordinate", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x????", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x%00y", "Bad LDAP URL: No Memory")]
    [DataRow("x???(cn=a%00)", "Bad LDAP URL: No Memory")]
    public async Task ExecuteAsync_WinLdapRefusedUrl_ConnectsSendsNothingAndFailsWith3(string path, string message)
    {
        var connection = new ScriptedConnection(Hex.Bytes(BindSuccess1));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));

        TransferResult result = await new LdapProtocolHandler(connector, LdapDialect.WinLdap).ExecuteAsync(SearchContext(path));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, message), result);
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("dc=example??bogus", "LDAP local: bad or missing scope")]
    [DataRow("x??onetree", "LDAP local: bad or missing scope")]
    [DataRow("dc=example?a?base?(cn=a)?x?y", "LDAP local: bad URL")]
    [DataRow("x????", "LDAP local: bad or missing extensions")]
    [DataRow("x???(cn=a%zz)", "LDAP local: bad or missing filter")]
    public async Task ExecuteAsync_OpenLdapRefusedUrl_FailsWith3WithoutConnecting(string path, string message)
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        TransferResult result = await new LdapProtocolHandler(connector, LdapDialect.OpenLdap).ExecuteAsync(SearchContext(path));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, message), result);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapUrlWithUserInformation_FailsWith3BadUrlWithoutConnecting()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var context = new TransferContext { Url = CurlUrl.Parse("ldap://u:p@127.0.0.1:38901/x"), Output = new MemoryStream() };

        TransferResult result = await new LdapProtocolHandler(connector, LdapDialect.OpenLdap).ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, "LDAP local: bad URL"), result);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    [DataRow("dc=example???(uid=a")]
    [DataRow("x???(cn=)")]
    [DataRow("x???(&)")]
    [DataRow("x???(cn=a)x")]
    public async Task ExecuteAsync_WinLdapRefusedFilter_UnbindsWithMessageId3AndFailsWith39FilterError(string path)
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.WinLdap, path, BindSuccess1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: Filter Error"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow("dc=example???(uid=a")]
    [DataRow("dc=example???(cn=a)(cn=b)")]
    [DataRow(@"x???(cn=\zz)")]
    [DataRow("x???(cn=a%00)")]
    public async Task ExecuteAsync_OpenLdapRefusedFilter_UnbindsWithMessageId3AndFailsWith39BadSearchFilter(string path)
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.OpenLdap, path, BindSuccess1);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP local: ldap_search_ext Bad search filter"), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSearchAnsweredNoSuchObject_UnbindsAndFailsWith39AndWinLdapsText()
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.WinLdap, "x", BindSuccess1, "30 10 02 01 02 65 0b 0a 01 20 04 00 04 04 6f 6f 70 73");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: No Such Object"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow(SearchNoSuchObject, "LDAP remote: search failed No such object ")]
    [DataRow("30 10 02 01 02 65 0b 0a 01 20 04 00 04 04 6f 6f 70 73", "LDAP remote: search failed No such object oops")]
    [DataRow("30 0c 02 01 02 65 07 0a 01 0f 04 00 04 00", "LDAP remote: search failed Unknown error ")]
    public async Task ExecuteAsync_OpenLdapSearchFailed_UnbindsAndFailsWith39AndLibLdapsTextAndDiagnostic(string done, string message)
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.OpenLdap, "x", BindSuccess1, done);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, message), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX + " " + OpenLdapUnbind3), sent);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_SearchAnsweredSizeLimitExceeded_Succeeds(LdapDialect dialect)
    {
        (TransferResult result, _) = await RunSearchAsync(dialect, "x", BindSuccess1, "30 0c 02 01 02 65 07 0a 01 04 04 00 04 00");

        Assert.AreEqual(TransferResult.Success(0), result);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_EntriesAndOtherMessagesBeforeTheDone_AreReadPast(LdapDialect dialect)
    {
        (TransferResult result, _) = await RunSearchAsync(
            dialect,
            "x",
            BindSuccess1,
            "30 0d 02 01 02 64 08 04 04 64 63 3d 78 30 00",
            "30 0c 02 01 07 65 07 0a 01 20 04 00 04 00",
            SearchNoSuchObject);

        Assert.AreEqual(CurlExitCode.LdapSearchFailed, result.ExitCode);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("30 05 02 01 02 04 00")]
    [DataRow("30 0c 02 01 07 65 07 0a 01 20 04 00 04 00")]
    [DataRow("04 00")]
    public async Task ExecuteAsync_WinLdapNoSearchResult_FailsWith39ServerDownWithoutUnbinding(string? reply)
    {
        (TransferResult result, byte[] sent) = reply is null
            ? await RunSearchAsync(LdapDialect.WinLdap, "x", BindSuccess1)
            : await RunSearchAsync(LdapDialect.WinLdap, "x", BindSuccess1, reply);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: Server Down"), result);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX), sent);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("30 0c 02 01 07 65 07 0a 01 20 04 00 04 00")]
    [DataRow("04 00")]
    [DataRow("30 03 04 01 00")]
    public async Task ExecuteAsync_OpenLdapNoSearchResult_FailsWith56WithoutUnbinding(string? reply)
    {
        (TransferResult result, byte[] sent) = reply is null
            ? await RunSearchAsync(LdapDialect.OpenLdap, "x", BindSuccess1)
            : await RunSearchAsync(LdapDialect.OpenLdap, "x", BindSuccess1, reply);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "LDAP local: search ldap_result Can't contact LDAP server"), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapOtherReplyToTheSearch_AbandonsItUnbindsAndSucceeds()
    {
        (TransferResult result, byte[] sent) = await RunSearchAsync(LdapDialect.OpenLdap, "x", BindSuccess1, "30 05 02 01 02 04 00");

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX + " 30 06 02 01 03 50 01 02 30 05 02 01 04 42 00"), sent);
    }

    /// <summary>Runs <paramref name="dialect" />'s handler with <c>-u cn=u:p</c> on <c>ldap://127.0.0.1:38901/</c> and <paramref name="path" />.</summary>
    private static async Task<(TransferResult Result, byte[] Sent)> RunSearchAsync(LdapDialect dialect, string path, params string[] replies)
    {
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect);

        TransferResult result = await handler.ExecuteAsync(SearchContext(path));

        return (result, connection.Sent);
    }

    private static TransferContext SearchContext(string path) => new()
    {
        Url = CurlUrl.Parse("ldap://127.0.0.1:38901/" + path),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential("cn=u", "p"),
    };
}
