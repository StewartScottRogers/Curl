using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Curl.Conformance;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5.Transports;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.Transports;

/// <summary>
/// Drives <see cref="Mqtt5TlsRefusalServer" /> directly with an <see cref="SslStream" /> client over an in-memory pair,
/// so each way curl can refuse the certificate is covered on every platform (BL-4596).
/// </summary>
[TestClass]
public sealed class Mqtt5TlsRefusalServerTests
{
    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ServeAsync_ClientFailsTheHandshake_RecordsTheRefusal()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Mqtt5TlsRefusalServer server = new(certificate);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task serving = server.ServeAsync(new ReferenceConnection(serverEnd, new WireExchange(8883, ReferenceTransport.Tcp)), CancellationToken.None);

        await clientEnd.WriteAsync("CONNECT is not a TLS record"u8.ToArray());
        await clientEnd.DisposeAsync();
        await serving;

        Assert.IsTrue(server.CurlRefusedTheCertificate);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ServeAsync_ClientClosesAfterTheHandshakeWithoutData_RecordsTheRefusal()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Mqtt5TlsRefusalServer server = new(certificate);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task serving = server.ServeAsync(new ReferenceConnection(serverEnd, new WireExchange(8883, ReferenceTransport.Tcp)), CancellationToken.None);

        SslStream client = await AuthenticateClientAsync(clientEnd);
        await client.DisposeAsync();
        await serving;

        Assert.IsTrue(server.CurlRefusedTheCertificate);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ServeAsync_ClientSendsDataAfterTheHandshake_FailsTheCase()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Mqtt5TlsRefusalServer server = new(certificate);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task serving = server.ServeAsync(new ReferenceConnection(serverEnd, new WireExchange(8883, ReferenceTransport.Tcp)), CancellationToken.None);

        await using SslStream client = await AuthenticateClientAsync(clientEnd);
        await client.WriteAsync(new byte[] { 0x10 });
        await client.FlushAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await serving);
        Assert.IsFalse(server.CurlRefusedTheCertificate);
    }

    private static async Task<SslStream> AuthenticateClientAsync(Stream clientEnd)
    {
        SslStream client = new(clientEnd, leaveInnerStreamOpen: false, static (_, _, _, _) => true);
        await client.AuthenticateAsClientAsync("localhost");
        return client;
    }
}
