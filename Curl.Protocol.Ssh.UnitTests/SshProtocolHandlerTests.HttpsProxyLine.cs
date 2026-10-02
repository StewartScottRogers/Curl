using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins curl 8.21.0's <c>SSH: using HTTPS proxy</c> line (<c>lib/vssh/libssh2.c</c>
/// <c>ssh_connect</c>): written right after <c>SSH: user '...'</c> when the HTTP proxy's
/// type is <c>CURLPROXY_HTTPS</c>, and for no other proxy and no proxy at all (BL-1124).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const string UserThenHttpsProxy = "* SSH: user 'tester' | * SSH: using HTTPS proxy | ";

    [TestMethod]
    public async Task ExecuteAsync_SftpThroughAnHttpsProxy_ReportsTheProxyLineAfterTheUser()
    {
        string lines = await RunThroughProxyRecordingLinesAsync("sftp://files.example/f", new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null));

        StringAssert.StartsWith(lines, $"* SSH: libssh2 cryptography backend: OpenSSL | {UserThenHttpsProxy}* SSH: no knownhosts file configured");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpThroughAnHttpsProxy_ReportsTheProxyLineAfterTheUser()
    {
        string lines = await RunThroughProxyRecordingLinesAsync("scp://files.example/f", new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null));

        StringAssert.StartsWith(lines, $"* SSH: libssh2 cryptography backend: OpenSSL | {UserThenHttpsProxy}* SSH: no knownhosts file configured");
    }

    [TestMethod]
    [DataRow(ProxyKind.Http)]
    [DataRow(ProxyKind.Socks5)]
    public async Task ExecuteAsync_ThroughAnotherProxyKind_ReportsNoProxyLine(ProxyKind kind)
    {
        string lines = await RunThroughProxyRecordingLinesAsync("sftp://files.example/f", new ProxyEndpoint(kind, "proxy.example", 1080, null));

        StringAssert.StartsWith(lines, $"{Start} | * SSH: no knownhosts file configured");
        StringAssert.DoesNotMatch(lines, new System.Text.RegularExpressions.Regex("HTTPS proxy"));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAProxy_ReportsNoProxyLine()
    {
        string lines = await RunThroughProxyRecordingLinesAsync("scp://files.example/f", proxy: null);

        StringAssert.StartsWith(lines, $"{Start} | * SSH: no knownhosts file configured");
        StringAssert.DoesNotMatch(lines, new System.Text.RegularExpressions.Regex("HTTPS proxy"));
    }

    private static async Task<string> RunThroughProxyRecordingLinesAsync(string url, ProxyEndpoint? proxy)
    {
        InMemorySshServer server = ServerWithAFile();
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = new System.Net.NetworkCredential(User, Password),
            Proxy = proxy,
            Events = events,
        };

        await Handler(server, files: null, agent: null).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();
        return string.Join(" | ", events.Transcript);
    }
}
