using System.Net;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <c>SSLKEYLOGFILE</c> (<see cref="TlsClientOptions.KeyLogFile" />, BL-2327): a TLS 1.2 handshake
/// appends its <c>CLIENT_RANDOM</c> line to the file, as curl's OpenSSL build does (upstream test2090).
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls12WithKeyLogFile_AppendsOneClientRandomLine()
    {
        var keyLogFile = Path.Combine(_directory, "2090.log.ssl");
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var provider = new HandBuiltTlsProvider(Tls12Only(new TlsClientOptions(Insecure: true, KeyLogFile: keyLogFile)), TimeProvider.System);
        Diagnostics.Arrange("key log file", "2090.log.ssl in a temporary directory");

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint, new IPEndPoint(IPAddress.Loopback, 50000)), CertificateHost, CancellationToken.None);
        await using var connection = result.Connection!;
        var lines = await File.ReadAllLinesAsync(keyLogFile);
        Diagnostics.Act("key log lines", string.Join(" | ", lines));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(1, lines);
        StringAssert.Matches(lines[0], new Regex("^CLIENT_RANDOM [0-9a-f]{64} [0-9a-f]{96}$"));
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }
}
