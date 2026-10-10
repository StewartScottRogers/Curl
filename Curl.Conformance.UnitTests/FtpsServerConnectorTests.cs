using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="FtpsServerConnector"/> serves the plain FTP control channel behind implicit TLS on <c>%FTPSPORT</c> and passes every other port on.</summary>
[TestClass]
public sealed class FtpsServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_AnotherPort_ReachesTheWrappedConnectorAsItIs()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        FtpsServerConnector connector = new(certificate, new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_FtpsPort_RelaysTheControlChannelDecrypted()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        FtpServerConnector ftp = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome 220 hi\nREPLY PBSZ 200 ok\n</servercmd>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));
        FtpsServerConnector connector = new(certificate, ftp);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpsServerConnector.FtpsPort, false), TestContext.CancellationToken);
        string greeting;
        string reply;
        await using (SslStream client = await MailTlsServerConnectorTests.HandshakeAsync(result.Connection!))
        {
            greeting = await MailTlsServerConnectorTests.ReadAsync(client, 8);
            await client.WriteAsync(Encoding.Latin1.GetBytes("PBSZ 0\r\n"), TestContext.CancellationToken);
            await client.FlushAsync(TestContext.CancellationToken);
            reply = await MailTlsServerConnectorTests.ReadAsync(client, 8);
        }

        Assert.AreEqual("220 hi\r\n", greeting);
        Assert.AreEqual("200 ok\r\n", reply);
        Assert.AreEqual("PBSZ 0\r\n", Encoding.Latin1.GetString(ftp.ReceivedBytes.Span));
        Assert.AreEqual(FtpsServerConnector.FtpsPort, ((IPEndPoint)result.Connection!.RemoteEndPoint!).Port);
    }

    [TestMethod]
    public void EmulatedServers_Always_NamesTheFtpsServer()
    {
        CollectionAssert.AreEqual(new[] { "ftps" }, FtpsServerConnector.EmulatedServers.ToArray());
    }
}
