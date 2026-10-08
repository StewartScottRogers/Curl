using System.Net;
using System.Text;
using Curl.Networking;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a TFTP transfer's connection is numbered with the run's other connections, as
/// curl 8.21.0 numbers it: two TFTP URLs shut down <c>#0</c> and then <c>#1</c>, and a TFTP URL
/// after an HTTP one whose connection was left intact shuts down <c>#1</c> (measured with
/// <c>Record-CurlExchange.ps1 -Tftp</c>, BL-969 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTftpConnectionNumberTests
{
    private static readonly IPEndPoint Server = new(IPAddress.Loopback, 69);

    private static readonly byte[] TftpData = [0, 3, 0, 1, (byte)'h', (byte)'i'];

    private readonly MemoryStream _standardOutput = new();

    private readonly MemoryStream _standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(_standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_TwoTftpUrls_ShutsDownConnection0ThenConnection1()
    {
        int exitCode = await RunAsync(new ScriptedConnector([]), "tftp://h/a", "tftp://h/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        int first = StandardErrorText.IndexOf("* shutting down connection #0", StringComparison.Ordinal);
        int second = StandardErrorText.IndexOf("* shutting down connection #1", StringComparison.Ordinal);
        Diagnostics.Assert("index of shutting down connection #0 is found", true, first >= 0);
        Assert.IsGreaterThanOrEqualTo(0, first);
        Diagnostics.Assert("shutting down connection #1 comes after #0", true, second > first);
        Assert.IsGreaterThan(first, second);
    }

    [TestMethod]
    public async Task RunAsync_TftpUrlAfterAnHttpUrlLeftIntact_ShutsDownConnection1()
    {
        byte[] response = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        Diagnostics.Bytes("scripted HTTP response", response);
        ScriptedConnector connector = new([response]);

        int exitCode = await RunAsync(connector, "http://h:18969/a", "tftp://h/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stderr has Connection #0 left intact", true, StandardErrorText.Contains("* Connection #0 to host h:18969 left intact", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "* Connection #0 to host h:18969 left intact");
        Diagnostics.Assert("stderr has shutting down connection #1", true, StandardErrorText.Contains("* shutting down connection #1", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "* shutting down connection #1");
        Diagnostics.Assert("stderr has shutting down connection #0", false, StandardErrorText.Contains("shutting down connection #0", StringComparison.Ordinal));
        Assert.DoesNotContain("shutting down connection #0", StandardErrorText);
    }

    private async Task<int> RunAsync(ScriptedConnector connector, params string[] urls)
    {
        Diagnostics.Arrange("arguments", "-s -v " + string.Join(' ', urls));
        Diagnostics.Bytes("scripted TFTP datagram", TftpData);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(
                    _standardOutput,
                    _standardError,
                    new MemoryStream(),
                    connector,
                    new ScriptedDatagramConnector(Server, Server, TftpData),
                    runConnections: new ConnectionCache(TimeProvider.System))
                .RunAsync(["-s", "-v", .. urls]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        return exitCode;
    }
}
