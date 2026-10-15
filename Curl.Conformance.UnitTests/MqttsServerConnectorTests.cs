using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="MqttsServerConnector"/> serves the plain MQTT stand-in behind TLS on <c>%MQTTSPORT</c> and passes every other port on.</summary>
[TestClass]
public sealed class MqttsServerConnectorTests
{
    // A CONNECT with client id "curl1234", as mqttd accepts it.
    private static readonly byte[] Connect = Convert.FromHexString("101400044d5154540402003c00086375726c31323334");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_AnotherPort_ReachesTheWrappedConnectorAsItIs()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        MqttsServerConnector connector = new(certificate, new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_MqttsPort_RelaysThePacketsDecrypted()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        UpstreamTestCase testCase = ParsedTestCase.From(string.Empty);
        MqttServerConnector mqtt = new(testCase, new SwsHttpServerConnector(testCase, TimeProvider.System));
        MqttsServerConnector connector = new(certificate, mqtt);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", MqttsServerConnector.MqttsPort, false), TestContext.CancellationToken);
        string connack;
        await using (SslStream client = await MailTlsServerConnectorTests.HandshakeAsync(result.Connection!))
        {
            await client.WriteAsync(Connect, TestContext.CancellationToken);
            await client.FlushAsync(TestContext.CancellationToken);
            connack = await MailTlsServerConnectorTests.ReadAsync(client, 4);
        }

        Assert.AreEqual("20020000", Convert.ToHexStringLower(Encoding.Latin1.GetBytes(connack)));
        Assert.AreEqual(
            "client CONNECT 14 00044d5154540402003c00086375726c31323334\nserver CONNACK 2 20020000\n",
            Encoding.Latin1.GetString(mqtt.ProtocolLog.Span));
        Assert.AreEqual(MqttsServerConnector.MqttsPort, ((IPEndPoint)result.Connection!.RemoteEndPoint!).Port);
    }

    [TestMethod]
    public void EmulatedServers_Always_NamesTheMqttsServer()
    {
        CollectionAssert.AreEqual(new[] { "mqtts" }, MqttsServerConnector.EmulatedServers.ToArray());
    }
}
