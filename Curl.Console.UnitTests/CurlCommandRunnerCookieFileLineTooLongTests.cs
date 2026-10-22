using Curl.Engine;
using System.Text;

using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.22.0's refusal of a <c>-b</c> file holding a line longer than 4998 characters before its line
/// feed: no request is sent and the transfer exits 100 with <c>A value or data field grew larger than
/// allowed</c>, while a 4998-character line loads and the transfer goes ahead. Measured 2026-10-21 with curl
/// 8.22.0 (mingw, Schannel) and <c>Record-CurlExchange.ps1</c> (BL-2916, BL-2971). No test opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerCookieFileLineTooLongTests
{
    private const string Url = "http://127.0.0.1:18231/";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string TooLargeError = "curl: (100) A value or data field grew larger than allowed";

    private const int LongestLine = 4998;

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string[] StandardErrorLines =>
        Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    [TestMethod]
    public async Task RunAsync_CookieFileWithA4999CharacterLine_FailsWithExit100BeforeConnecting()
    {
        fileSystem.ExistingContent["cf.txt"] = Encoding.Latin1.GetBytes(CookieLine(LongestLine + 1) + "\n");
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);

        int exitCode = await RunWithAsync(server, ["-S", "-s", "-b", "cf.txt", Url]);

        Diagnostics.Assert("exit code", 100, exitCode);
        Diagnostics.Assert("stderr lines", string.Join(" | ", new[] { TooLargeError, string.Empty }), string.Join(" | ", StandardErrorLines));
        Diagnostics.Assert("connections made", 0, server.Targets.Count());
        Assert.AreEqual(100, exitCode);
        CollectionAssert.AreEqual(new[] { TooLargeError, string.Empty }, StandardErrorLines);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_CookieFileWithA4998CharacterLine_LoadsItAndSendsTheRequest()
    {
        string line = CookieLine(LongestLine);
        fileSystem.ExistingContent["cf.txt"] = Encoding.Latin1.GetBytes(line + "\n");
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);

        int exitCode = await RunWithAsync(server, ["-s", "-b", "cf.txt", Url]);

        string request = Encoding.Latin1.GetString(server.Written);
        string expectedCookie = $"Cookie: name={line[(line.LastIndexOf('\t') + 1)..]}\r\n";
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("request has the file's cookie", true, request.Contains(expectedCookie, StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.Contains(expectedCookie, request);
    }

    /// <summary>A Netscape cookie line for 127.0.0.1, <paramref name="length" /> characters long before its line feed.</summary>
    private static string CookieLine(int length)
    {
        const string Start = "127.0.0.1\tFALSE\t/\tFALSE\t0\tname\t";
        return Start + new string('v', length - Start.Length);
    }

    private async Task<int> RunWithAsync(IConnector connector, string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    options => CreateTransferDispatch(connector, options),
                    fileSystem,
                    fileSystem,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    /// <summary>The production handler set over <paramref name="connector" />, with the run's cookies.</summary>
    private static TransferDispatch CreateTransferDispatch(IConnector connector, Cli.CommandLineOptions options)
    {
        CookieEngine? cookies = CookieEngine.FromHttpOptions(EasyOptionsFromCommandLine.HttpOf(options));
        IReadOnlyList<IProtocolHandler> handlers = EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies?.HandlerStore);

        return new TransferDispatch(new ProtocolDispatcher(handlers), [], cookies);
    }
}
