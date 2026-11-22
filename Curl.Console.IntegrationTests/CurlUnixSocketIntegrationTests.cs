using System.Net.Sockets;
using System.Text;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>curl --unix-socket &lt;path&gt; http://localhost:8080/hello.txt</c> against a loopback HTTP server
/// on a Unix socket: the body arrives and curl exits 0, on Windows, Linux and macOS alike (BL-5686).
/// </summary>
/// <remarks>
/// The socket path is absolute and longer than 14 bytes on purpose: on macOS a .NET listener on a
/// shorter path dies with "Destination is too short" on its first accept from any client, upstream
/// curl included (BL-5686 Notes), which is a fault of the server, not of the client under test.
/// </remarks>
[TestClass]
public sealed class CurlUnixSocketIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_UnixSocketToALoopbackHttpServer_ExitsZeroWithTheBody()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl5686-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        try
        {
            Diagnostics.Arrange("socket", path);
            var serving = ServeOneResponseAsync(listener, TestContext.CancellationToken);
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: false)
                .RunAsync(["-sS", "--unix-socket", path, "http://localhost:8080/hello.txt"]);
            var request = await serving;

            var body = Encoding.ASCII.GetString(standardOutput.ToArray());
            Diagnostics.Act("exit code, body, stderr", $"{exitCode}, {body}, {Encoding.UTF8.GetString(standardError.ToArray())}");
            Diagnostics.Assert("exit code", 0, exitCode);
            Assert.AreEqual(0, exitCode);
            Assert.AreEqual("hello", body);
            Assert.StartsWith("GET /hello.txt HTTP/1.1\r\nHost: localhost:8080\r\n", request);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> ServeOneResponseAsync(Socket listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptAsync(cancellationToken);
        var request = new StringBuilder();
        var buffer = new byte[4096];
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
            if (read == 0)
            {
                break;
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        await client.SendAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"u8.ToArray(), SocketFlags.None, cancellationToken);
        return request.ToString();
    }
}
