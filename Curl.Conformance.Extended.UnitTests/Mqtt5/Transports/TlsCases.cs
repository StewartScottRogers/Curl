using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.Transports;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.Transports;

/// <summary>
/// Runs curl's <c>mqtt5s://</c> against the MQTT 5 reference broker behind TLS and checks the client rules of
/// OASIS MQTT Version 5.0 chapter 5 (BL-4486): the server's certificate is verified unless <c>-k</c> is given.
/// </summary>
[TestClass]
public sealed class TlsCases
{
    private const int Port = 8883;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-5.4.5-1")]
    public async Task Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Mqtt5TlsRefusalServer server = new(certificate);

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "10", "mqtt5s://localhost/t", "-d", "hi"]);

        Assert.AreEqual(60, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (60) ");
        Assert.IsEmpty(result.StandardOutput);
        Assert.IsNotNull(server.HandshakeFailure);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-5.4.5-2")]
    [SpecRequirement("Mqtt5:MQTT5-4.2-1")]
    public async Task Mqtt5s_InsecureFlag_RunsTheSameMqttExchangeOverTlsOnPort8883()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Mqtt5ReferenceBroker broker = new();

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(Port, broker, certificate)], ["-sS", "-k", "-m", "10", "mqtt5s://localhost/t", "-d", "hi"]);

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.IsEmpty(result.StandardError);
        Assert.AreEqual(ReferenceTransport.Tls, result.Exchanges.Single().Transport);
        CollectionAssert.AreEqual(
            new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Publish, Mqtt5WirePacket.Disconnect },
            broker.ReceivedPackets.Select(static packet => packet.PacketType).ToArray());
    }

    [TestMethod]
    public async Task RefusalServer_HandshakeCurlCompletes_FailsTheRun()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Mqtt5TlsRefusalServer server = new(certificate);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-k", "-m", "10", "mqtt5s://localhost/t", "-d", "hi"]));

        Assert.IsNull(server.HandshakeFailure);
    }
}
