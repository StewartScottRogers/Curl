using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-290: the Schannel build says <c>reusing existing credential handle</c> (through
/// <see cref="TlsTrustEvent.ReusesCredentialHandle" />) on a second handshake to a peer, as curl
/// 8.22.0's Schannel build does; the OpenSSL build says it on neither.
/// </summary>
[TestClass]
public sealed class Cgr290Tests
{
    private static readonly IPEndPoint FirstServer = new(IPAddress.Loopback, 443);

    [TestMethod]
    public async Task Handshake_SchannelBuildSecondTimeToThePeer_ReusesTheCredentialHandle()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild: true);

        var first = await TrustOfHandshakeAsync(provider, FirstServer, "localhost");
        var second = await TrustOfHandshakeAsync(provider, FirstServer, "LOCALHOST");

        Assert.IsFalse(first.ReusesCredentialHandle);
        Assert.IsTrue(second.ReusesCredentialHandle);
    }

    [TestMethod]
    public async Task Handshake_SchannelBuildToAnotherPeer_AcquiresANewCredentialHandle()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild: true);

        await TrustOfHandshakeAsync(provider, FirstServer, "localhost");
        var other = await TrustOfHandshakeAsync(provider, new IPEndPoint(IPAddress.Loopback, 444), "localhost");

        Assert.IsFalse(other.ReusesCredentialHandle);
    }

    [TestMethod]
    public async Task Handshake_OpenSslBuildSecondTimeToThePeer_SaysNothingOfACredentialHandle()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild: false);

        await TrustOfHandshakeAsync(provider, FirstServer, "localhost");
        var second = await TrustOfHandshakeAsync(provider, FirstServer, "localhost");

        Assert.IsFalse(second.ReusesCredentialHandle);
    }

    // The handshake fails at once against a closed server; the trust is reported before it starts.
    private static async Task<TlsTrustEvent> TrustOfHandshakeAsync(SslStreamTlsProvider provider, IPEndPoint server, string host)
    {
        var (client, serverSide) = InMemoryDuplexStream.CreatePair();
        serverSide.Dispose();
        var events = new RecordingTransferEvents();

        await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, server, new IPEndPoint(IPAddress.Loopback, 50000)), host, events, false, ["http/1.1"], CancellationToken.None);

        return events.TlsEvents.OfType<TlsTrustEvent>().Single();
    }
}
