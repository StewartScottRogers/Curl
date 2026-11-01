using Curl.Engine;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the progress meter of a <c>-T</c> upload from standard input ends, through
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />. curl 8.22.0 reads
/// <c>-T .</c> without blocking under its own progress callback, so <c>Curl_pgrsDone</c> writes no
/// newline after the meter's last status line, while <c>-T -</c> ends with one (upstream test1498,
/// BL-2170).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerStandardInputUploadProgressMeterTests
{
    private const string Url = "http://127.0.0.1:18247/bzz/1498";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_UploadFromStandardInputWithDot_EndsTheMeterWithoutANewline()
    {
        int exitCode = await RunAsync("-T", ".", Url);

        Diagnostics.Assert("stderr ends with", "a status line, no newline", Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(StandardErrorText.EndsWith('\n'));
        Assert.StartsWith("100     15   0      0   0     15      0", StandardErrorText[(StandardErrorText.LastIndexOf('\r') + 1)..]);
    }

    [TestMethod]
    public async Task RunAsync_UploadFromStandardInputWithDash_EndsTheMeterWithANewline()
    {
        int exitCode = await RunAsync("-T", "-", Url);

        Diagnostics.Assert("stderr ends with", "a newline", Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.EndsWith(Environment.NewLine, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Runs <paramref name="arguments" /> with five bytes on standard input and
    /// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" /> answering 200.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        InMemoryFileSystem outputFiles = new();
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream("hello"u8.ToArray()),
                    runsOnWindows: false,
                    writesProgressMeter: true,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
