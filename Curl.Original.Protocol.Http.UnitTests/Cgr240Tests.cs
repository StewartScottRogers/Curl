using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-240 (b): measured on curl 8.22.0 (Windows), <c>-i -o o.txt</c> against a
/// <c>HTTP/1.1 099 OK</c> reply exits 1 and <c>o.txt</c> holds the head, so the head reaches the output
/// before the code is refused.
/// </summary>
[TestClass]
public sealed class Cgr240Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Code099WithBody_WritesTheHeadAndFailsUnsupportedProtocol()
    {
        const string head = "HTTP/1.1 099 OK\r\nContent-Length: 6\r\n\r\n";
        MemoryStream output = new();
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(head + "-foo-\n"), 65536, null);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/1432"), Output = output, HeaderOutput = output });

        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(head, Encoding.Latin1.GetString(output.ToArray()));
    }
}
