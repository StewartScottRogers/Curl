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

    // BL-2339: a TLS 1.3 handshake appends its five secrets in OpenSSL's order, offered beside
    // TLS 1.2 or alone. The SslStream test server cannot speak TLS 1.3 on macOS (BL-1176).
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls13)]
    public async Task AuthenticateAsClientAsync_Tls13WithKeyLogFile_AppendsTheFiveTrafficSecretLines(TlsVersion minimumVersion)
    {
        var keyLogFile = Path.Combine(_directory, "tls13.log.ssl");
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls13);
        var provider = new HandBuiltTlsProvider(new TlsClientOptions(Insecure: true, MinimumVersion: minimumVersion, KeyLogFile: keyLogFile), TimeProvider.System);
        Diagnostics.Arrange("minimum version", minimumVersion);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint, new IPEndPoint(IPAddress.Loopback, 50000)), CertificateHost, CancellationToken.None);
        await using var connection = result.Connection!;
        var lines = await File.ReadAllLinesAsync(keyLogFile);
        Diagnostics.Act("key log lines", string.Join(" | ", lines));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        string[] labels = ["CLIENT_HANDSHAKE_TRAFFIC_SECRET", "SERVER_HANDSHAKE_TRAFFIC_SECRET", "CLIENT_TRAFFIC_SECRET_0", "SERVER_TRAFFIC_SECRET_0", "EXPORTER_SECRET"];
        Assert.HasCount(labels.Length, lines);
        var clientRandom = lines[0].Split(' ')[1];
        StringAssert.Matches(clientRandom, new Regex("^[0-9a-f]{64}$"));
        for (var index = 0; index < labels.Length; index++)
        {
            StringAssert.Matches(lines[index], new Regex($"^{labels[index]} {clientRandom} [0-9a-f]{{64}}([0-9a-f]{{32}})?$"));
        }

        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }
}
