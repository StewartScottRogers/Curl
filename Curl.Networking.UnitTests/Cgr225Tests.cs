using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-225 (BL-3754): the handshake event carries the <c>--cacert</c> file and the number of
/// certificates it added, so the Schannel build's <c>-v</c> can print
/// <c>schannel: added N certificate(s) from CA file</c>.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFile_ReportsTheFileAndItsCertificateCount()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: true), SchannelBuild);
        var events = new RecordingTransferEvents();
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = leaf });
            _ = await sslStream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
        });

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(caFile, events.Handshakes[0].CaCertificateFile);
        Assert.AreEqual(1, events.Handshakes[0].CaCertificateCount);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }
}
