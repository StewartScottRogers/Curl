using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v -b ''</c> end to end (BL-3177). Measured on curl 8.22.0 (mingw, Schannel) on 2026-10-22 with
/// <c>Record-CurlExchange.ps1</c>: <c>curl -v -L -b '' -c - http://127.0.0.1:&lt;port&gt;/one</c> against a 302
/// carrying <c>Set-Cookie: a=b</c> wrote no cookie file warning, sent <c>Cookie: a=b</c> on the redirect and exited 0,
/// while <c>-b nosuchfile.txt</c> still wrote <c>* WARNING: failed to open cookie file "nosuchfile.txt"</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerEmptyCookieFileTests
{
    private const string Head = "Host: 127.0.0.1:18231\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n";

    private const string RedirectSetsCookie =
        "HTTP/1.1 302 Found\r\nLocation: /2\r\nSet-Cookie: got=g1\r\nContent-Length: 0\r\n\r\n";

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    [TestMethod]
    public async Task RunAsync_VerboseEmptyCookieFile_PrintsNoWarningAndSendsTheFirstHopsCookie()
    {
        ScriptedConnector server = new(new[] { RedirectSetsCookie, "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n" }.Select(Encoding.Latin1.GetBytes));

        int exitCode = await RunAsync(server, ["-s", "-v", "-L", "-b", "", "http://127.0.0.1:18231/1"]);

        Assert.AreEqual(0, exitCode);
        Assert.DoesNotContain("failed to open cookie file", Encoding.Latin1.GetString(standardError.ToArray()));
        Assert.StartsWith(
            $"GET /1 HTTP/1.1\r\n{Head}\r\nGET /2 HTTP/1.1\r\n{Head}Cookie: got=g1\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
    }

    private Task<int> RunAsync(IConnector connector, string[] arguments) =>
        new CurlCommandRunner(
                options => CreateTransferDispatch(connector, options),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(arguments);

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
