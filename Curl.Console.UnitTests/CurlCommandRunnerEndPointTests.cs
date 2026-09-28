using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>%{local_ip}</c>, <c>%{local_port}</c>, <c>%{remote_ip}</c> and <c>%{remote_port}</c>
/// for every scheme, through the production handler set over fake connectors, against
/// curl 8.21.0 measured with <c>Record-CurlExchange.ps1</c> (BL-515 Notes; ADR-0119).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerEndPointTests
{
    private const string EndPointsTemplate = "%{local_ip} %{local_port} %{remote_ip} %{remote_port}\\n";

    private static readonly IPEndPoint FailingDatagramServer = new(IPAddress.Loopback, 1);

    [TestMethod]
    public async Task RunAsync_FtpDownload_ReportsTheControlConnectionsEndPoints()
    {
        // curl -s -w "..." ftp://127.0.0.1:47515/f.txt printed "hello127.0.0.1 64513 127.0.0.1 47515":
        // the control connection's ends, not the data connection's.
        EndPointScriptedConnector connector = new(
            new EndPointScriptedConnector.Script(
                Loopback(64513),
                Loopback(47515),
                Latin1(
                    "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
                    + "229 Entering Extended Passive Mode (|||64512|)\r\n200 Type set\r\n213 5\r\n"
                    + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n")),
            new EndPointScriptedConnector.Script(Loopback(64514), Loopback(64512), Latin1("hello")));

        (int exitCode, string output) = await RunAsync("ftp://127.0.0.1:47515/f.txt", connector, FailingDatagramConnector());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello127.0.0.1 64513 127.0.0.1 47515\n", output);
    }

    [TestMethod]
    public async Task RunAsync_DictLookup_ReportsTheConnectionsEndPoints()
    {
        // curl -s -w "..." dict://127.0.0.1:47516/d:word printed "220 hi\r\n127.0.0.1 64515 127.0.0.1 47516".
        EndPointScriptedConnector connector = new(
            new EndPointScriptedConnector.Script(Loopback(64515), Loopback(47516), Latin1("220 hi\r\n")));

        (int exitCode, string output) = await RunAsync("dict://127.0.0.1:47516/d:word", connector, FailingDatagramConnector());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("220 hi\r\n127.0.0.1 64515 127.0.0.1 47516\n", output);
    }

    [TestMethod]
    public async Task RunAsync_TftpDownload_ReportsTheServerAndNoLocalEnd()
    {
        // curl -s -w "..." tftp://127.0.0.1:47519/f printed "hi 0 127.0.0.1 47519": the URL's server,
        // not the port the reply came from, and no local end for its unconnected UDP socket.
        ScriptedDatagramConnector datagramConnector = new(Loopback(47519), Loopback(62775), [0, 3, 0, 1, (byte)'h', (byte)'i']);

        (int exitCode, string output) = await RunAsync("tftp://127.0.0.1:47519/f", new EndPointScriptedConnector(), datagramConnector);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hi 0 127.0.0.1 47519\n", output);
    }

    [TestMethod]
    public async Task RunAsync_FtpConnectFails_ReportsNoEndPoints()
    {
        // curl -s -w "..." ftp://127.0.0.1:47518/f against a closed port exited 7 and printed " -1  -1".
        (int exitCode, string output) = await RunAsync("ftp://127.0.0.1:47518/f", new EndPointScriptedConnector(), FailingDatagramConnector());

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(" -1  -1\n", output);
    }

    [TestMethod]
    public async Task RunAsync_HttpTransfer_ReportsTheHandlersOwnEndPoints()
    {
        EndPointScriptedConnector connector = new(
            new EndPointScriptedConnector.Script(Loopback(62095), Loopback(18227), Latin1("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")));

        (int exitCode, string output) = await RunAsync("http://127.0.0.1:18227/", connector, FailingDatagramConnector());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hi127.0.0.1 62095 127.0.0.1 18227\n", output);
    }

    [TestMethod]
    public async Task RunAsync_TwoUrls_EachReportsItsOwnConnection()
    {
        EndPointScriptedConnector connector = new(
            new EndPointScriptedConnector.Script(Loopback(64515), Loopback(47516), Latin1("220 a\r\n")),
            new EndPointScriptedConnector.Script(Loopback(64516), Loopback(47517), Latin1("220 b\r\n")));

        (int exitCode, string output) = await RunAsync(
            ["-s", "-w", EndPointsTemplate, "dict://127.0.0.1:47516/d:a", "dict://127.0.0.1:47517/d:b"],
            connector,
            FailingDatagramConnector());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("220 a\r\n127.0.0.1 64515 127.0.0.1 47516\n220 b\r\n127.0.0.1 64516 127.0.0.1 47517\n", output);
    }

    private static IPEndPoint Loopback(int port) => new(IPAddress.Loopback, port);

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static RecordingDatagramConnector FailingDatagramConnector() =>
        new(CurlExitCode.CouldntConnect, $"Failed to connect to {FailingDatagramServer}");

    private static Task<(int ExitCode, string Output)> RunAsync(string url, IConnector connector, IDatagramConnector datagramConnector) =>
        RunAsync(["-s", "-w", EndPointsTemplate, url], connector, datagramConnector);

    private static async Task<(int ExitCode, string Output)> RunAsync(string[] arguments, IConnector connector, IDatagramConnector datagramConnector)
    {
        using MemoryStream standardOutput = new();

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(
                    CurlComposition.CreateProtocolHandlers(connector, datagramConnector, new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);

        return (exitCode, Encoding.Latin1.GetString(standardOutput.ToArray()));
    }
}
