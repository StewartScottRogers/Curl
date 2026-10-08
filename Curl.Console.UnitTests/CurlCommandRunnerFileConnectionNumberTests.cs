using System.Text;
using Curl.Networking;
using Curl.Testing;

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

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(_standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_FileUrlAfterAnHttpUrlLeftIntact_ShutsDownConnection1()
    {
        string fileUrl = CreateTemporaryFileUrl();

        int exitCode = await RunAsync("http://h:18977/a", fileUrl);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("connection #0 left intact", true, StandardErrorText.Contains("* Connection #0 to host h:18977 left intact", StringComparison.Ordinal));
        Diagnostics.Assert("shutting down connection #1", true, StandardErrorText.Contains("* shutting down connection #1", StringComparison.Ordinal));
        Diagnostics.Assert("shutting down connection #0", false, StandardErrorText.Contains("shutting down connection #0", StringComparison.Ordinal));
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
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("file shutdown line found", true, file >= 0);
        Diagnostics.Assert("http left-intact line after the file shutdown", true, http > file);
        Assert.IsGreaterThanOrEqualTo(0, file);
        Assert.IsGreaterThan(file, http);
    }

    private string CreateTemporaryFileUrl()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl977-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "a.txt");
        File.WriteAllText(path, "hello");
        Diagnostics.Arrange("temporary file", Path.GetFileName(path) + " containing hello");
        return new Uri(path).AbsoluteUri;
    }

    private async Task<int> RunAsync(params string[] urls)
    {
        Diagnostics.Arrange("urls", string.Join(" ", urls.Select(url => url.StartsWith("file:", StringComparison.Ordinal) ? "file:///<temporary>/" + Path.GetFileName(url) : url)));
        Diagnostics.Arrange("server", "one scripted HTTP 200 response, body ok");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(
                    _standardOutput,
                    _standardError,
                    new MemoryStream(),
                    new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]),
                    new RecordingDatagramConnector(Protocol.Abstractions.CurlExitCode.CouldntConnect, "unused"),
                    runConnections: new ConnectionCache(TimeProvider.System))
                .RunAsync(["-s", "-v", .. urls]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", _standardError.ToArray());

        return exitCode;
    }
}
