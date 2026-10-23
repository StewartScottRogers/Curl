using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>curl http://[::ffff:127.0.0.1]:&lt;port&gt;/</c> against a loopback HTTP server listening on
/// IPv4 only: curl 8.22.0 connects through the IPv4-mapped literal and exits 0 with the body (measured
/// 2026-10-22, BL-3174 Notes, ADR-0775).
/// </summary>
[TestClass]
public sealed class CurlMappedIPv4LiteralIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_MappedIPv4LiteralOfAnIpV4Server_ExitsZeroWithTheBody()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var url = $"http://[::ffff:127.0.0.1]:{port}/echo";
        Diagnostics.Arrange("url", url);
        var serving = ServeOneResponseAsync(listener, TestContext.CancellationToken);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: false)
            .RunAsync(["-sS", url]);
        var request = await serving;

        var body = Encoding.ASCII.GetString(standardOutput.ToArray());
        Diagnostics.Act("exit code, body, stderr", $"{exitCode}, {body}, {Encoding.UTF8.GetString(standardError.ToArray())}");
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", body);
        Assert.Contains($"Host: [::ffff:127.0.0.1]:{port}\r\n", request);
    }

    private static async Task<string> ServeOneResponseAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        var request = new StringBuilder();
        var buffer = new byte[4096];
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"u8.ToArray(), cancellationToken);
        return request.ToString();
    }
}
