using System.Text;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins that a <c>file://</c> transfer is numbered with the run's other connections, as curl
/// 8.21.0 numbers it: after an HTTP connection left intact as <c>#0</c> the file transfer shuts
/// down <c>#1</c>, and an HTTP connection after a file transfer is <c>#1</c> (measured with
/// <c>Record-CurlExchange.ps1</c>, BL-977 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerFileConnectionNumberTests
{
    private readonly MemoryStream _standardOutput = new();

    private readonly MemoryStream _standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private string StandardErrorText => Encoding.UTF8.GetString(_standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_FileUrlAfterAnHttpUrlLeftIntact_ShutsDownConnection1()
    {
        string fileUrl = CreateTemporaryFileUrl();

        int exitCode = await RunAsync("http://h:18977/a", fileUrl);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "* Connection #0 to host h:18977 left intact");
        StringAssert.Contains(StandardErrorText, "* shutting down connection #1");
        Assert.DoesNotContain("shutting down connection #0", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_HttpUrlAfterAFileUrl_NumbersTheHttpConnection1()
    {
        string fileUrl = CreateTemporaryFileUrl();

        int exitCode = await RunAsync(fileUrl, "http://h:18977/a");

        Assert.AreEqual(0, exitCode);
        int file = StandardErrorText.IndexOf("* shutting down connection #0", StringComparison.Ordinal);
        int http = StandardErrorText.IndexOf("* Connection #1 to host h:18977 left intact", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, file);
        Assert.IsGreaterThan(file, http);
    }

    private string CreateTemporaryFileUrl()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl977-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        TestContext.WriteLine(directory);
        string path = Path.Combine(directory, "a.txt");
        File.WriteAllText(path, "hello");
        return new Uri(path).AbsoluteUri;
    }

    private Task<int> RunAsync(params string[] urls) =>
        CurlComposition
            .CreateRunner(
                _standardOutput,
                _standardError,
                new MemoryStream(),
                new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]),
                new RecordingDatagramConnector(Protocol.Abstractions.CurlExitCode.CouldntConnect, "unused"),
                runConnections: new ConnectionCache(TimeProvider.System))
            .RunAsync(["-s", "-v", .. urls]);
}
