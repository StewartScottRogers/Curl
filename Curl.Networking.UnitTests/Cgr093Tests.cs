using System.Net;
using System.Net.Security;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-093 of the third-party gap report (BL-3622): curl sends the URL host as SNI lowercased
/// and without a trailing dot (RFC 6066 section 3), so <c>https://LOCALHOST:p/</c>,
/// <c>https://LocalHost:p/</c> and <c>https://localhost.:p/</c> all send <c>localhost</c>
/// (ADR-0825).
/// </summary>
[TestClass]
public sealed class Cgr093Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("LOCALHOST", "localhost")]
    [DataRow("LocalHost", "localhost")]
    [DataRow("localhost.", "localhost")]
    [DataRow("Example.COM.", "example.com")]
    [DataRow("localhost", "localhost")]
    public void ServerNameIndication_LowercasesAndDropsTheTrailingDot(string targetHost, string expected)
    {
        Diagnostics.Arrange("target host", targetHost);

        var serverName = SslStreamTlsProvider.ServerNameIndication(targetHost);

        Diagnostics.Act("server name", serverName);
        Assert.AreEqual(expected, serverName);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AuthenticateAsClientAsync_MixedCaseHostWithTrailingDot_SendsTheLowercasedNameWithoutTheDot(bool matchesSchannelBuild)
    {
        SslClientAuthenticationOptions? handshakeOptions = null;
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild)
        {
            AuthenticateSslStreamAsClientAsync = (_, authenticationOptions, _) =>
            {
                handshakeOptions = authenticationOptions;
                throw new AuthenticationException("The test stops before the handshake.");
            },
        };
        var (client, _) = InMemoryDuplexStream.CreatePair();

        await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, new IPEndPoint(IPAddress.Loopback, 443)), "LocalHost.", CancellationToken.None);

        Diagnostics.Act("target host", handshakeOptions?.TargetHost);
        Assert.AreEqual("localhost", handshakeOptions!.TargetHost);
    }
}
