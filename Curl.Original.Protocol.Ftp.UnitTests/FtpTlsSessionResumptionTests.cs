using System.Text;

using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Both FTP TLS handshakes name the control connection as their session source, so the data
/// connection resumes its session, as vsftpd's <c>require_ssl_reuse</c> demands (STD-220, ADR-1468).
/// The TLS double refuses a data handshake that does not resume, as vsftpd does.
/// </summary>
[TestClass]
public sealed class FtpTlsSessionResumptionTests
{
    private const string LogInAndProtect =
        "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n" +
        "257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||64396|)\r\n200 Type set\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ExplicitTlsDownload_NamesTheControlConnectionAsTheSessionSourceOfBothHandshakes()
    {
        var plaintextControl = ScriptedConnection.FromReplies("220 ready\r\n234 Proceed\r\n");
        var securedControl = ScriptedConnection.FromReplies(
            LogInAndProtect + "213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n");
        var tls = RequiringReuse(securedControl, ScriptedConnection.FromReplies("hello"));
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1/a.txt"), Output = output, SslLevel = TransportSecurityLevel.Required };

        TransferResult result = await Handler(plaintextControl, tls).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(output.ToArray()));
        Assert.HasCount(2, tls.SessionSources);
        Assert.AreSame(plaintextControl, tls.SessionSources[0]);
        Assert.AreSame(securedControl, tls.SessionSources[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExplicitTlsUploadToAServerRequiringSessionReuse_StoresTheWholeFile()
    {
        var plaintextControl = ScriptedConnection.FromReplies("220 ready\r\n234 Proceed\r\n");
        var securedControl = ScriptedConnection.FromReplies(
            LogInAndProtect + "150 Ok to send data\r\n226 Transfer complete\r\n221 Bye\r\n");
        var securedData = new ScriptedConnection();
        var tls = RequiringReuse(securedControl, securedData);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1/up2.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.Latin1.GetBytes("hello from vsftpd")),
            SslLevel = TransportSecurityLevel.Required,
        };

        TransferResult result = await Handler(plaintextControl, tls).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("hello from vsftpd", Encoding.Latin1.GetString(securedData.Sent));
        Assert.AreSame(securedControl, tls.SessionSources[1]);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public Task ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing() =>
        AssertListingOverResumedSessionWritesAsync("hello.txt\r\n");

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public Task ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuseOffWindows_WritesTheListingWithLfEndings() =>
        AssertListingOverResumedSessionWritesAsync("hello.txt\n");

    private static async Task AssertListingOverResumedSessionWritesAsync(string expectedOutput)
    {
        var plaintextControl = ScriptedConnection.FromReplies("220 ready\r\n234 Proceed\r\n");
        var securedControl = ScriptedConnection.FromReplies(
            LogInAndProtect + "150 Here comes the directory listing\r\n226 Directory send OK\r\n221 Bye\r\n");
        var tls = RequiringReuse(securedControl, ScriptedConnection.FromReplies("hello.txt\r\n"));
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1/"), Output = output, SslLevel = TransportSecurityLevel.Required };

        TransferResult result = await Handler(plaintextControl, tls).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(expectedOutput, Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreSame(securedControl, tls.SessionSources[1]);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_DataHandshakeThatDoesNotResumeOnAServerRequiringSessionReuse_IsRefused()
    {
        var tls = RequiringReuse(new ScriptedConnection(), new ScriptedConnection());
        await tls.AuthenticateAsClientResumingSessionOfAsync(new ScriptedConnection(), new ScriptedConnection(), "h", NoTransferEvents.Instance, CancellationToken.None);

        ConnectResult plain = await tls.AuthenticateAsClientAsync(new ScriptedConnection(), "h", NoTransferEvents.Instance, CancellationToken.None);
        ConnectResult otherSource = await tls.AuthenticateAsClientResumingSessionOfAsync(new ScriptedConnection(), new ScriptedConnection(), "h", NoTransferEvents.Instance, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, plain.ExitCode);
        Assert.AreEqual(QueuedTlsProvider.SessionReuseRequiredMessage, plain.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SslConnectError, otherSource.ExitCode);
    }

    private static QueuedTlsProvider RequiringReuse(IConnection securedControl, IConnection securedData) =>
        new(ConnectResult.Connected(securedControl), ConnectResult.Connected(securedData)) { RequiresSessionReuse = true };

    private static FtpProtocolHandler Handler(IConnection plaintextControl, QueuedTlsProvider tls) =>
        new(new QueuedConnector(ConnectResult.Connected(plaintextControl), ConnectResult.Connected(new ScriptedConnection())), new QueuedListener(), tls);
}
