namespace Curl.Console;

/// <summary>
/// Pins the CONNECT reply head in <c>-i</c>, <c>-I</c> and <c>-D</c> output, and
/// <c>--suppress-connect-headers</c> leaving it out, end to end through a real
/// <see cref="Curl.Networking.TcpConnector" />. Every expected byte was measured on 2026-09-30
/// with curl 8.21.0 (mingw, Schannel) against <c>Record-CurlExchange.ps1</c> as the proxy
/// (BL-613 Notes).
/// </summary>
public sealed partial class CurlCompositionProxyTests
{
    private const string EstablishedWithHeader = "HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\n";

    private const string ConnectRequest =
        "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    [TestMethod]
    [DataRow("-i", DisplayName = "-i")]
    [DataRow("--include", DisplayName = "--include")]
    public async Task RunAsync_ProxyTunnelWithInclude_WritesTheConnectReplyHeadBeforeTheResponse(string include)
    {
        // curl -sS -p -x 127.0.0.1:18613 -i http://example.com/
        ScriptedConnector server = new([Latin1(EstablishedWithHeader), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", include, "http://example.com/a");

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", ConnectRequest + TunnelledGet, Latin1(server.Written));
        Assert.AreEqual(ConnectRequest + TunnelledGet, Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", EstablishedWithHeader + Hello, run.StandardOutput);
        Assert.AreEqual(EstablishedWithHeader + Hello, run.StandardOutput);
    }

    [TestMethod]
    [DataRow(new[] { "-i", "--suppress-connect-headers" }, DisplayName = "-i --suppress-connect-headers")]
    [DataRow(new[] { "--suppress-connect-headers", "--no-suppress-connect-headers", "--suppress-connect-headers", "-i" }, DisplayName = "the last spelling wins")]
    public async Task RunAsync_ProxyTunnelWithSuppressConnectHeaders_LeavesTheConnectReplyHeadOut(string[] options)
    {
        // curl -sS -p -x 127.0.0.1:18613 -i --suppress-connect-headers http://example.com/
        ScriptedConnector server = new([Latin1(EstablishedWithHeader), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, ["-sS", "-p", "-x", "127.0.0.1:18238", .. options, "http://example.com/a"]);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", ConnectRequest + TunnelledGet, Latin1(server.Written));
        Assert.AreEqual(ConnectRequest + TunnelledGet, Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", Hello, run.StandardOutput);
        Assert.AreEqual(Hello, run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithNoSuppressConnectHeaders_WritesTheConnectReplyHead()
    {
        ScriptedConnector server = new([Latin1(EstablishedWithHeader), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "--suppress-connect-headers", "--no-suppress-connect-headers", "-i", "http://example.com/a");

        Diagnostics.Assert("run.StandardOutput", EstablishedWithHeader + Hello, run.StandardOutput);
        Assert.AreEqual(EstablishedWithHeader + Hello, run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithDumpHeaderToStandardOutput_WritesTheConnectReplyHeadThere()
    {
        // curl -sS -p -x 127.0.0.1:18613 -D - http://example.com/
        ScriptedConnector server = new([Latin1(EstablishedWithHeader), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "-D", "-", "http://example.com/a");

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("run.StandardOutput", EstablishedWithHeader + Hello, run.StandardOutput);
        Assert.AreEqual(EstablishedWithHeader + Hello, run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithDumpHeaderAndSuppressConnectHeaders_LeavesTheConnectReplyHeadOut()
    {
        // curl -sS -p -x 127.0.0.1:18613 -D - --suppress-connect-headers http://example.com/
        ScriptedConnector server = new([Latin1(EstablishedWithHeader), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "-D", "-", "--suppress-connect-headers", "http://example.com/a");

        Diagnostics.Assert("run.StandardOutput", Hello, run.StandardOutput);
        Assert.AreEqual(Hello, run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithHead_WritesTheConnectReplyHeadBeforeTheResponseHead()
    {
        // curl -sS -p -x 127.0.0.1:18613 -I http://example.com/
        ScriptedConnector server = new([Latin1(EstablishedWithHeader), Latin1("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n")]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "-I", "http://example.com/a");

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            ConnectRequest + "HEAD /a HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", EstablishedWithHeader + "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", run.StandardOutput);
        Assert.AreEqual(EstablishedWithHeader + "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_Proxy10TunnelWithInclude_SendsHttp10ConnectAndWritesTheReplyHead()
    {
        // curl -sS -p --proxy1.0 127.0.0.1:18613 -i http://example.com/, the proxy answering HTTP/1.0 200 OK
        ScriptedConnector server = new([Latin1("HTTP/1.0 200 OK\r\n\r\n"), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "--proxy1.0", "127.0.0.1:18238", "-i", "http://example.com/a");

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.0\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n" + TunnelledGet,
            Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", "HTTP/1.0 200 OK\r\n\r\n" + Hello, run.StandardOutput);
        Assert.AreEqual("HTTP/1.0 200 OK\r\n\r\n" + Hello, run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelRefusedWithInclude_WritesTheRefusalsHeadAndExitsSeven()
    {
        // curl -sS -p -x 127.0.0.1:18613 -i http://example.com/, the proxy answering 403
        ScriptedConnector server = new([Latin1("HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\nno\n")]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "-i", "http://example.com/a");

        Diagnostics.Assert("run.ExitCode", 7, run.ExitCode);
        Assert.AreEqual(7, run.ExitCode);
        Diagnostics.Assert("run.StandardOutput", "HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\n", run.StandardOutput);
        Assert.AreEqual("HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\n", run.StandardOutput);
        Diagnostics.Assert("run.StandardError", ($"curl: (7) CONNECT tunnel failed, response 403{Environment.NewLine}").ReplaceLineEndings("\n"), run.StandardError.ReplaceLineEndings("\n"));
        Assert.AreEqual($"curl: (7) CONNECT tunnel failed, response 403{Environment.NewLine}", run.StandardError);
    }
}
