using Curl.Testing;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the <c>-v</c> lines each build writes for an LDAP transfer, recorded with
/// <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-29 (BL-589): curl 8.21.0 with WinLDAP on
/// Windows and curl 8.18.0 with OpenLDAP 2.6.10 under WSL, <c>-sv -u cn=u,dc=x:secret</c>,
/// the bind answered, then one entry <c>dc=example</c> with <c>ou: x</c> and a
/// SearchResultDone. The connector's own <c>Trying</c> and <c>Established connection</c>
/// lines are not the handler's.
/// </summary>
public sealed partial class LdapProtocolHandlerTests
{
    private const string Url = "ldap://127.0.0.1:18389/dc=example";

    private const string EntryOuX2 = "30 1e 02 01 02 64 19 04 0a 64 63 3d 65 78 61 6d 70 6c 65 30 0b 30 09 04 02 6f 75 31 03 04 01 78";

    private const string SearchSizeLimitExceeded2 = "30 0c 02 01 02 65 07 0a 01 04 04 00 04 00";

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSearch_ReportsTheVendorTheUrlTheConnectionTheDataAndShutsDown()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.WinLdap, Url, BindSuccess1, EntryOuX2, SearchDone2);

        Diagnostics.Assert("vendor line", LdapVerboseLines.WinLdapVendor, lines[0]);
        Diagnostics.Assert("last line", "shutting down connection #0", lines[^1]);
        Assert.AreEqual(LdapVerboseLines.WinLdapVendor, lines[0]);
        Assert.AreEqual("LDAP local: ldap://127.0.0.1:18389/dc=example", lines[1]);
        Assert.AreEqual("LDAP local: trying to establish cleartext connection", lines[2]);
        Assert.AreEqual("{ [4 bytes data]", lines[3]);
        Assert.IsTrue(lines[3..^1].All(line => line.StartsWith('{')));
        Assert.AreEqual("shutting down connection #0", lines[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapsSearch_SaysTheConnectionIsEncrypted()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.WinLdap, "ldaps://127.0.0.1:18636/dc=example", BindSuccess1, SearchDone2);

        Diagnostics.Assert("line count", 4, lines.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                LdapVerboseLines.WinLdapVendor,
                "LDAP local: ldaps://127.0.0.1:18636/dc=example",
                "LDAP local: trying to establish encrypted connection",
                "shutting down connection #0",
            },
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapBindRefused_ReportsTheFailureThenShutsDown()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.WinLdap, Url, BindInvalidCredentials1, "30 0c 02 01 02 61 07 0a 01 31 04 00 04 00");

        Diagnostics.Assert("line count", 5, lines.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                LdapVerboseLines.WinLdapVendor,
                "LDAP local: ldap://127.0.0.1:18389/dc=example",
                "LDAP local: trying to establish cleartext connection",
                "LDAP local: bind via ldap_win_bind Invalid Credentials",
                "shutting down connection #0",
            },
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapBadUrl_ReportsTheVendorTheUrlAndTheFailureThenShutsDown()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.WinLdap, "ldap://127.0.0.1:18389/dc=example??bogus");

        Diagnostics.Assert("line count", 4, lines.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                LdapVerboseLines.WinLdapVendor,
                "LDAP local: ldap://127.0.0.1:18389/dc=example??bogus",
                "Bad LDAP URL: Invalid Syntax",
                "shutting down connection #0",
            },
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapSearch_ReportsTheUrlOnceBoundTheDataAndLeavesTheConnectionIntact()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.OpenLdap, Url, BindSuccess1, EntryOuX2, SearchDone2);

        Diagnostics.Assert("first line", "LDAP local: ldap://127.0.0.1:18389/dc=example", lines[0]);
        Diagnostics.Assert("last line", "Connection #0 to host 127.0.0.1:18389 left intact", lines[^1]);
        Assert.AreEqual("LDAP local: ldap://127.0.0.1:18389/dc=example", lines[0]);
        Assert.AreEqual("{ [4 bytes data]", lines[1]);
        Assert.IsTrue(lines[1..^1].All(line => line.StartsWith('{')));
        Assert.AreEqual("Connection #0 to host 127.0.0.1:18389 left intact", lines[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapLoginDenied_OnlyClosesTheConnection()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.OpenLdap, Url, BindInvalidCredentials1);

        Diagnostics.Assert("line count", 1, lines.Count);
        CollectionAssert.AreEqual(new[] { "closing connection #0" }, lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapSearchFailed_ReportsTheUrlAndTheFailureThenClosesTheConnection()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.OpenLdap, Url, BindSuccess1, "30 0c 02 01 02 65 07 0a 01 20 04 00 04 00");

        Diagnostics.Assert("line count", 3, lines.Count);
        CollectionAssert.AreEqual(
            new[] { "LDAP local: ldap://127.0.0.1:18389/dc=example", "LDAP remote: search failed No such object ", "closing connection #0" },
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapBadScope_ReportsTheFailureAndClosesConnectionMinusOne()
    {
        List<string> lines = await VerboseLinesAsync(LdapDialect.OpenLdap, "ldap://127.0.0.1:18389/dc=x?a?bogus");

        Diagnostics.Assert("line count", 2, lines.Count);
        CollectionAssert.AreEqual(new[] { "LDAP local: bad or missing scope", "closing connection #-1" }, lines);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_SizeLimitExceededAfterTwoEntries_ReportsMoreThanTwoEntriesAfterTheEntries(LdapDialect dialect)
    {
        List<string> lines = await VerboseLinesAsync(dialect, Url, BindSuccess1, EntryOuX2, EntryOuX2, SearchSizeLimitExceeded2);

        int moreThan = lines.IndexOf("There are more than 2 entries");
        Diagnostics.Act("more-than line index", moreThan);
        Diagnostics.Assert("more-than lines", 1, lines.Count(line => line.StartsWith("There are more than", StringComparison.Ordinal)));
        Assert.IsTrue(moreThan > lines.FindLastIndex(line => line.StartsWith('{')), string.Join(" | ", lines));
        Assert.AreEqual(1, lines.Count(line => line.StartsWith("There are more than", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_SizeLimitExceededWithNoEntries_ReportsMoreThanZeroEntries(LdapDialect dialect)
    {
        List<string> lines = await VerboseLinesAsync(dialect, Url, BindSuccess1, SearchSizeLimitExceeded2);

        Diagnostics.Assert("contains more-than-zero line", true, lines.Contains("There are more than 0 entries"));
        CollectionAssert.Contains(lines, "There are more than 0 entries");
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_SearchDoneWithSuccess_ReportsNoMoreThanLine(LdapDialect dialect)
    {
        List<string> lines = await VerboseLinesAsync(dialect, Url, BindSuccess1, EntryOuX2, SearchDone2);

        Diagnostics.Assert("has more-than line", false, lines.Any(line => line.StartsWith("There are more than", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.StartsWith("There are more than", StringComparison.Ordinal)), string.Join(" | ", lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_Verbose_ReportsTheConnectToTheTransfersEvents()
    {
        var events = new RecordingTransferEvents();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Hex.Bytes(BindSuccess1 + " " + SearchDone2))));

        await new LdapProtocolHandler(connector, LdapDialect.OpenLdap).ExecuteAsync(VerboseContext(Url, events));

        Diagnostics.Act("target", connector.Targets.Single());
        Diagnostics.Assert("events is the transfer's", true, ReferenceEquals(events, connector.Targets.Single().Events));
        Assert.AreSame(events, connector.Targets.Single().Events);
    }

    [TestMethod]
    [DataRow("ldap://127.0.0.1:18389/dc=example", "ldap://127.0.0.1:18389/dc=example")]
    [DataRow("LDAP://a:b@127.0.0.1:18389?x??bogus#frag", "ldap://a:b@127.0.0.1:18389/?x??bogus#frag")]
    [DataRow("ldap://LocalHost:389/%41?x??bogus", "ldap://LocalHost:389/%41?x??bogus")]
    [DataRow("ldap://h", "ldap://h/")]
    [DataRow("ldap://h#f", "ldap://h/#f")]
    [DataRow("ldap.example.com/dc=x", "ldap://ldap.example.com/dc=x")]
    public void Url_TypedUrl_IsTheUrlAsCurlHoldsIt(string typed, string held)
    {
        Diagnostics.Arrange("typed", typed);
        Diagnostics.Act("url line", LdapVerboseLines.Url(CurlUrl.Parse(typed)));
        Diagnostics.Assert("url line", "LDAP local: " + held, LdapVerboseLines.Url(CurlUrl.Parse(typed)));
        Assert.AreEqual("LDAP local: " + held, LdapVerboseLines.Url(CurlUrl.Parse(typed)));
    }

    private async Task<List<string>> VerboseLinesAsync(LdapDialect dialect, string url, params string[] replies)
    {
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);

        Diagnostics.Arrange("dialect", dialect);
        Diagnostics.Arrange("replies", string.Join(" | ", replies));

        await new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect).ExecuteAsync(VerboseContext(url, events));

        Diagnostics.Act("verbose lines", string.Join(" | ", events.Lines));
        return events.Lines;
    }

    private TransferContext VerboseContext(string url, RecordingTransferEvents events)
    {
        Diagnostics.Arrange("url", url);
        return new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = User,
            Events = events,
        };
    }
}
