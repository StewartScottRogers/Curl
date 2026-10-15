using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="GophersServerConnector"/> serves the plain Gopher stand-in behind TLS on <c>%GOPHERSPORT</c> and passes every other port on.</summary>
[TestClass]
public sealed class GophersServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_AnotherPort_ReachesTheWrappedConnectorAsItIs()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        GophersServerConnector connector = new(certificate, new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_GophersPort_RelaysTheSelectorDecrypted()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        UpstreamTestCase testCase = ParsedTestCase.From("<reply>\n<data>\nmenu\n</data>\n</reply>\n");
        GopherServerConnector gopher = new(testCase, new SwsHttpServerConnector(testCase, TimeProvider.System));
        GophersServerConnector connector = new(certificate, gopher);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", GophersServerConnector.GophersPort, false), TestContext.CancellationToken);
        string answer;
        await using (SslStream client = await MailTlsServerConnectorTests.HandshakeAsync(result.Connection!))
        {
            await client.WriteAsync(Encoding.Latin1.GetBytes("/1272\r\n"), TestContext.CancellationToken);
            await client.FlushAsync(TestContext.CancellationToken);
            answer = await MailTlsServerConnectorTests.ReadAsync(client, 5);
        }

        Assert.AreEqual("menu\n", answer);
        Assert.AreEqual("/1272\r\n", Encoding.Latin1.GetString(gopher.ReceivedBytes.Span));
        Assert.AreEqual(GophersServerConnector.GophersPort, ((IPEndPoint)result.Connection!.RemoteEndPoint!).Port);
    }

    [TestMethod]
    public void EmulatedServers_Always_NamesTheGophersServer()
    {
        CollectionAssert.AreEqual(new[] { "gophers" }, GophersServerConnector.EmulatedServers.ToArray());
    }
}
