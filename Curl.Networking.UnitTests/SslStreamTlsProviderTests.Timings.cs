using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the <see cref="ConnectTimings" /> a successful handshake reports, taken from the
/// <see cref="TimeProvider" /> the provider is given.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new SslStreamTlsProvider(new TlsClientOptions(), null!));

        Assert.AreEqual("timeProvider", exception.ParamName);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheHandshakeSucceeds_RecordsWhenItBeganAndWhenItCompleted()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), new SteppingTimeProvider(500));

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new ConnectTimings(500, null, 500, 510), result.Timings);
        Assert.IsNull(result.LocalEndPoint);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }
}
