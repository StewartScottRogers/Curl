using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the lines an <c>ldap://</c> transfer writes to Curl's diagnostic log under the
/// <c>ldap</c> component (ADR-0222, BL-929): the bind, the search's base, scope and filter,
/// the entries returned and the transfer's end at <c>info</c>, the failure that ends the
/// transfer at <c>error</c>, and each message sent and search reply received at
/// <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class LdapTransferLogTests
{
    private const string BindSuccess1 = "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00";

    private const string BindInvalidCredentials1 = "30 0c 02 01 01 61 07 0a 01 31 04 00 04 00";

    // A SearchResultEntry for messageID 2: cn=a with cn: a.
    private const string Entry2 = "30 18 02 01 02 64 13 04 04 63 6e 3d 61 30 0b 30 09 04 02 63 6e 31 03 04 01 61";

    private const string SearchDone2 = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    private static NetworkCredential User => new("cn=u,dc=x", "s3cret");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_BindAndSearch_LogsTheBindTheSearchTheEntriesAndTheEndAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "ldap://127.0.0.1:18389/dc=example");
        Diagnostics.Arrange("replies", "bind success, entry, search done");

        await Handler(BindSuccess1, Entry2, SearchDone2).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", User, log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Diagnostics.Act("info lines", string.Join(" | ", info));
        Diagnostics.Assert("info line count", 4, info.Length);
        Assert.HasCount(4, info);
        Assert.AreEqual("bound with a simple bind as \"cn=u,dc=x\"", info[0]);
        Assert.AreEqual("search base \"dc=example\", scope baseObject, filter (objectClass=*)", info[1]);
        Assert.AreEqual("search returned 1 entries", info[2]);
        Assert.StartsWith("transfer done: ", info[3]);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Ldap));
    }

    [TestMethod]
    public async Task ExecuteAsync_ScopeAndFilter_LogsThemAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "ldap://127.0.0.1:18389/dc=example?cn?sub?(uid=a)");
        Diagnostics.Arrange("replies", "bind success, search done");

        await Handler(BindSuccess1, SearchDone2).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example?cn?sub?(uid=a)", null, log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Diagnostics.Act("info lines", string.Join(" | ", info));
        Diagnostics.Assert("search line", "search base \"dc=example\", scope wholeSubtree, filter (uid=a)", info[1]);
        Assert.AreEqual("bound anonymously", info[0]);
        Assert.AreEqual("search base \"dc=example\", scope wholeSubtree, filter (uid=a)", info[1]);
        Assert.AreEqual("search returned 0 entries", info[2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BindAndSearch_LogsEachMessageIdAndOperationAtVerbose()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "ldap://127.0.0.1:18389/dc=example");
        Diagnostics.Arrange("replies", "bind success, entry, search done");

        await Handler(BindSuccess1, Entry2, SearchDone2).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", User, log));

        string[] verbose = log.MessagesAt(DiagnosticLogLevel.Verbose);
        Diagnostics.Act("verbose lines", string.Join(" | ", verbose));
        Diagnostics.Assert("verbose line count", 5, verbose.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "sent BindRequest with message ID 1",
                "sent SearchRequest with message ID 2",
                "received SearchResultEntry for message ID 2",
                "received SearchResultDone for message ID 2",
                "sent UnbindRequest with message ID 3",
            },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidCredentials_LogsTheExitCodeAtError()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "ldap://127.0.0.1:18389/dc=example");
        Diagnostics.Arrange("bind reply", BindInvalidCredentials1);

        TransferResult result = await Handler(BindInvalidCredentials1).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", User, log));

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error lines", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Error)));
        Diagnostics.Assert("exit code", CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "transfer failed with LoginDenied (exit 67): Login denied" },
            log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_LogsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Error);
        Diagnostics.Arrange("url", "ldap://127.0.0.1:18389/dc=example");

        await Handler(BindSuccess1, Entry2, SearchDone2).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", User, log));

        Diagnostics.Act("line count", log.Lines.Count);
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_LogsNothingOnFailure()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.None);
        Diagnostics.Arrange("bind reply", BindInvalidCredentials1);

        await Handler(BindInvalidCredentials1).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", User, log));

        Diagnostics.Act("line count", log.Lines.Count);
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Hex.Bytes(BindSuccess1), Hex.Bytes(SearchDone2))));

        Diagnostics.Arrange("url", "ldap://127.0.0.1:18389/dc=example");

        await new LdapProtocolHandler(connector, LdapDialect.OpenLdap).ExecuteAsync(Context("ldap://127.0.0.1:18389/dc=example", User, log));

        Diagnostics.Act("connect targets", connector.Targets.Count);
        Diagnostics.Assert("target log is the context log", true, ReferenceEquals(log, connector.Targets.Single().DiagnosticLog));
        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_SimpleBindPassword_IsNeverLogged()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "ldap://cn=u:<password>@127.0.0.1:18389/dc=example");

        await Handler(BindSuccess1, Entry2, SearchDone2).ExecuteAsync(Context("ldap://cn=u:s3cret@127.0.0.1:18389/dc=example", User, log));

        bool leaked = log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal));
        Diagnostics.Act("line count", log.Lines.Count);
        Diagnostics.Assert("password appears in a line", false, leaked);
        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow((int)LdapSearchReplyKind.OtherResponse, "received another response for message ID 2")]
    [DataRow((int)LdapSearchReplyKind.OtherMessage, "received a message for another request for message ID 2")]
    [DataRow((int)LdapSearchReplyKind.Lost, "received nothing: the server closed or sent no LDAPMessage for message ID 2")]
    public void SearchReplyReceived_OtherKinds_DescribesThem(int kind, string expected)
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("reply kind", (LdapSearchReplyKind)kind);

        new LdapTransferLog(log).SearchReplyReceived(LdapSearchReply.Of((LdapSearchReplyKind)kind), 2);

        Diagnostics.Act("verbose lines", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        Diagnostics.Assert("verbose text", expected, string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { expected }, log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    private static LdapProtocolHandler Handler(params string[] replies) =>
        new(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection([.. replies.Select(Hex.Bytes)]))), LdapDialect.OpenLdap);

    private static TransferContext Context(string url, NetworkCredential? credentials, IDiagnosticLog log) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Credentials = credentials,
        DiagnosticLog = log,
    };
}
