using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that <see cref="InProcessCurl.RunAsync(IReadOnlyList{string}, Stream, Stream, Stream, ITcpDialer, IDnsResolver, IDatagramConnector, bool)" />
/// answers a Digest challenge in the form of the build it is told to match: the conformance
/// harness's non-SSPI form by default, and on Windows, with <c>usesHandBuiltNtlm</c> off (as the
/// <c>curl-mcp</c> tool does), the Schannel build's SSPI form that <c>curl.exe</c> sends (BL-5685).
/// </summary>
[TestClass]
public sealed class InProcessCurlDigestBuildTests
{
    private const string Challenge =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n1\", qop=\"auth\"\r\nContent-Length: 0\r\n\r\n";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_UsesHandBuiltNtlmOffOnWindows_SendsTheSspiFormDigestHeader()
    {
        string request = await RunDigestAsync(usesHandBuiltNtlm: false);

        StringAssert.Contains(request, "Authorization: Digest username=\"user\",realm=\"r\",nonce=\"n1\",uri=\"/p\",cnonce=\"");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_UsesHandBuiltNtlmByDefault_SendsTheNonSspiFormDigestHeader()
    {
        string request = await RunDigestAsync(usesHandBuiltNtlm: null);

        StringAssert.Contains(request, "Authorization: Digest username=\"user\", realm=\"r\", nonce=\"n1\", uri=\"/p\", cnonce=\"");
    }

    private async Task<string> RunDigestAsync(bool? usesHandBuiltNtlm)
    {
        ScriptedConnector server = new([Encoding.ASCII.GetBytes(Challenge), Encoding.ASCII.GetBytes(Ok)]) { ReusesTheFirstConnection = true };
        ScriptedTcpDialer dialer = new(server);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        IReadOnlyList<string> arguments = ["-s", "--digest", "-u", "user:pass", "http://127.0.0.1:18247/p"];
        IDnsResolver resolver = new LiteralDnsResolver();
        RecordingDatagramConnector datagrams = new(CurlExitCode.CouldntConnect, "unused");

        int exitCode = usesHandBuiltNtlm is { } value
            ? await InProcessCurl.RunAsync(arguments, standardOutput, standardError, new MemoryStream(), dialer, resolver, datagrams, value)
            : await InProcessCurl.RunAsync(arguments, standardOutput, standardError, new MemoryStream(), dialer, resolver, datagrams);

        Assert.AreEqual(0, exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
        return Encoding.ASCII.GetString(server.Written);
    }

    private sealed class LiteralDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<System.Net.IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<System.Net.IPAddress>>([System.Net.IPAddress.Loopback]);
    }
}
