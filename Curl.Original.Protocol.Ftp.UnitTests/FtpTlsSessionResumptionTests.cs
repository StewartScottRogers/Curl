using System.Text;

using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Both FTP TLS handshakes name the control connection as their session source, so the data
/// connection resumes its session, as vsftpd's <c>require_ssl_reuse</c> demands (STD-220, ADR-1418).
/// </summary>
[TestClass]
public sealed class FtpTlsSessionResumptionTests
{
    [TestMethod]
    public async Task ExecuteAsync_ExplicitTlsDownload_NamesTheControlConnectionAsTheSessionSourceOfBothHandshakes()
    {
        var plaintextControl = ScriptedConnection.FromReplies("220 ready\r\n234 Proceed\r\n");
        var securedControl = ScriptedConnection.FromReplies(
            "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n" +
            "257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||64396|)\r\n200 Type set\r\n213 5\r\n" +
            "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n");
        var tls = new QueuedTlsProvider(
            ConnectResult.Connected(securedControl),
            ConnectResult.Connected(ScriptedConnection.FromReplies("hello")));
        var connector = new QueuedConnector(
            ConnectResult.Connected(plaintextControl),
            ConnectResult.Connected(new ScriptedConnection()));
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1/a.txt"), Output = output, SslLevel = TransportSecurityLevel.Required };

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(output.ToArray()));
        Assert.HasCount(2, tls.SessionSources);
        Assert.AreSame(plaintextControl, tls.SessionSources[0]);
        Assert.AreSame(securedControl, tls.SessionSources[1]);
    }
}
