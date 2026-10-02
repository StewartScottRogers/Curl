using System.Net;
using System.Text;
using Curl.Networking;

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

    private readonly MemoryStream _standardOutput = new();

    private readonly MemoryStream _standardError = new();

    private string StandardErrorText => Encoding.UTF8.GetString(_standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_TwoTftpUrls_ShutsDownConnection0ThenConnection1()
    {
        int exitCode = await RunAsync(new ScriptedConnector([]), "tftp://h/a", "tftp://h/b");

        Assert.AreEqual(0, exitCode);
        int first = StandardErrorText.IndexOf("* shutting down connection #0", StringComparison.Ordinal);
        int second = StandardErrorText.IndexOf("* shutting down connection #1", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, first);
        Assert.IsGreaterThan(first, second);
    }

    [TestMethod]
    public async Task RunAsync_TftpUrlAfterAnHttpUrlLeftIntact_ShutsDownConnection1()
    {
        ScriptedConnector connector = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]);

        int exitCode = await RunAsync(connector, "http://h:18969/a", "tftp://h/b");

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "* Connection #0 to host h:18969 left intact");
        StringAssert.Contains(StandardErrorText, "* shutting down connection #1");
        Assert.DoesNotContain("shutting down connection #0", StandardErrorText);
    }

    private Task<int> RunAsync(ScriptedConnector connector, params string[] urls) =>
        CurlComposition
            .CreateRunner(
                _standardOutput,
                _standardError,
                new MemoryStream(),
                connector,
                new ScriptedDatagramConnector(Server, Server, [0, 3, 0, 1, (byte)'h', (byte)'i']),
                runConnections: new ConnectionCache(TimeProvider.System))
            .RunAsync(["-s", "-v", .. urls]);
}
