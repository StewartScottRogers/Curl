using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="TcpConnector" /> dials the Unix domain socket of <c>--unix-socket</c> and
/// <c>--abstract-unix-socket</c> (BL-507), against curl 8.21.0 (mingw, Schannel) measured on
/// 2026-09-28 with <c>Record-CurlExchange.ps1 -UnixSocket</c> (BL-507 Notes): <c>-v --unix-socket
/// C:\Users\Public\s.sock http://localhost/a</c> printed <c>*   Trying C:\Users\Public\s.sock:0...</c>
/// and <c>* Established connection to C:\Users\Public\s.sock (C:\Users\Public\s.sock port 0) from
/// port 0 </c>; a missing socket printed <c>Immediate connect fail for s.sock: Connection
/// refused</c>, <c>connect to s.sock port 0 from  port 0 failed: Connection refused</c> and exit 7
/// <c>Failed to connect to localhost:80 over unix://s.sock after 0 ms: Could not connect to server</c>.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocket_DialsTheSocketAndResolvesNothing()
    {
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var socket = new UnixSocketAddress("/run/app.sock", IsAbstract: false);
        var connector = CreateUnixSocketConnector(resolver, dialer, new FakeTlsProvider(), socket);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 8080, UseTls: false));

        Diagnostics.Assert("dialed unix sockets", 1, dialer.DialedUnixSockets.Count);
        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(new[] { socket }, dialer.DialedUnixSockets);
        Assert.IsEmpty(dialer.DialedEndPoints);
        Assert.IsEmpty(resolver.ResolvedHosts);
        Assert.IsNull(result.LocalEndPoint);
        Assert.AreEqual(0, result.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocket_ReturnsTheWholePathForTheLeftIntactLine()
    {
        // * Connection #0 to host <path, lower-cased>:0 left intact names the whole path, not
        // the 45 characters of Trying (measured, BL-794 Notes); the HTTP handler lower-cases it.
        const string path = @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock";
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress(path, IsAbstract: false));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 8080, UseTls: false));

        Diagnostics.Assert("unix socket path", path, result.UnixSocketPath);
        Assert.AreEqual(path, result.UnixSocketPath);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnAbstractUnixSocket_ReturnsTheNameAsGiven()
    {
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress("app", IsAbstract: true));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 80, UseTls: false));

        Diagnostics.Assert("unix socket path", "app", result.UnixSocketPath);
        Assert.AreEqual("app", result.UnixSocketPath);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocket_ReportsTryingAndTheConnectionOpenedAsCurlDoes()
    {
        // *   Trying C:\Users\Public\s.sock:0...
        // * Established connection to C:\Users\Public\s.sock (C:\Users\Public\s.sock port 0) from  port 0
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress(@"C:\Users\Public\s.sock", IsAbstract: false));

        await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("info lines", @"  Trying C:\Users\Public\s.sock:0...", string.Join("\n", events.Info));
        CollectionAssert.AreEqual(new[] { @"  Trying C:\Users\Public\s.sock:0..." }, events.Info);
        Assert.AreEqual(
            new ConnectionOpenedEvent
            {
                HostName = @"C:\Users\Public\s.sock",
                RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0),
                LocalEndPoint = new IPEndPoint(IPAddress.Any, 0),
                UnixSocketRemoteIp = @"C:\Users\Public\s.sock",
                ConnectionNumber = 0,
            },
            events.Opened.Single());
    }

    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocketPathLongerThan45Characters_ShowsItCutTo45InTrying()
    {
        // *   Trying C:\Users\Stewart Rogers\AppData\Local\Temp\bl:0... for ...\Temp\bl507.sock (measured).
        const string path = @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock";
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress(path, IsAbstract: false));

        await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 8080, UseTls: false) { Events = events });

        Diagnostics.Assert("info lines", @"  Trying C:\Users\Stewart Rogers\AppData\Local\Temp\bl:0...", string.Join("\n", events.Info));
        CollectionAssert.AreEqual(new[] { @"  Trying C:\Users\Stewart Rogers\AppData\Local\Temp\bl:0..." }, events.Info);
        Assert.AreEqual(path, events.Opened.Single().HostName);
        Assert.AreEqual(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl", events.Opened.Single().UnixSocketRemoteIp);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheUnixSocketRefuses_FailsWithExit7AndCurlsLines()
    {
        var events = new RecordingTransferEvents();
        var time = new ManualTimeProvider();
        var dialer = new FakeTcpDialer
        {
            UnixSocketDialOutcome = _ =>
            {
                time.Advance(3);
                throw new SocketException((int)SocketError.ConnectionRefused);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), time, unixSocket: new UnixSocketAddress("s.sock", IsAbstract: false));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to localhost:80 over unix://s.sock after 3 ms: Could not connect to server", result.ErrorMessage);
        Assert.AreEqual(0, result.ConnectionNumber);
        Assert.IsNull(result.Timings!.Connected);
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying s.sock:0...",
                $"Immediate connect fail for s.sock: {ConnectFailureReason.Describe(new SocketException((int)SocketError.ConnectionRefused), OperatingSystem.IsWindows())}",
                $"connect to s.sock port 0 from  port 0 failed: {ConnectFailureReason.Describe(new SocketException((int)SocketError.ConnectionRefused), OperatingSystem.IsWindows())}",
                "Failed to connect to localhost:80 over unix://s.sock after 3 ms: Could not connect to server",
            },
            events.Info);
        Assert.IsEmpty(events.Opened);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_WhenTheUnixSocketsDirectoryIsMissingOnWindows_SaysNetworkDown()
    {
        // curl -v --unix-socket C:/nope/x.sock http://localhost/ -> Immediate connect fail for C:/nope/x.sock: Network down
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => throw new SocketException((int)SocketError.NetworkDown) };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress("C:/nope/x.sock", IsAbstract: false));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsFalse(result.IsConnectionRefused);
        Assert.AreEqual("Immediate connect fail for C:/nope/x.sock: Network down", events.Info[1]);
        Assert.AreEqual("connect to C:/nope/x.sock port 0 from  port 0 failed: Network down", events.Info[2]);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_WithAnAbstractSocketOnWindows_FailsAsCurlsInvalidArguments()
    {
        // curl -v --abstract-unix-socket x http://localhost/ (Windows) -> *   Trying :0...
        // * Immediate connect fail for : Invalid arguments
        // * connect to  port 0 from  port 0 failed: Invalid arguments
        // curl: (7) Failed to connect to localhost:80 over unix://x after 0 ms: Could not connect to server
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => throw new SocketException((int)SocketError.InvalidArgument) };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress("x", IsAbstract: true));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to localhost:80 over unix://x after 0 ms: Could not connect to server", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying :0...",
                "Immediate connect fail for : Invalid arguments",
                "connect to  port 0 from  port 0 failed: Invalid arguments",
                "Failed to connect to localhost:80 over unix://x after 0 ms: Could not connect to server",
            },
            events.Info);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task ConnectAsync_WithAnAbstractSocketOnLinux_ShowsNoNameAndConnects()
    {
        // curl 8.18.0 (Ubuntu, OpenSSL) -v --abstract-unix-socket abs1 http://localhost/ -> *   Trying :0...
        // * Established connection to localhost ( port 0) from  port 0 (8.21.0 names the socket instead)
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress("abs1", IsAbstract: true));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("info lines", "  Trying :0...", string.Join("\n", events.Info));
        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(new[] { "  Trying :0..." }, events.Info);
        Assert.AreEqual(string.Empty, events.Opened.Single().UnixSocketRemoteIp);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocketPathTooLong_FailsWithExit6AndDialsNothing()
    {
        // curl --unix-socket <108 a's> http://localhost/ -> curl: (6) Unix socket path too long: '<108 a's>'
        var path = new string('a', 108);
        var dialer = new FakeTcpDialer();
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress(path, IsAbstract: false));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual($"Unix socket path too long: '{path}'", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedUnixSockets);
        Assert.AreEqual(0, result.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocketAndAProxy_DialsTheSocketAndSendsNoConnect()
    {
        // curl -x http://127.0.0.1:9/ --unix-socket /tmp/l.sock http://example.com/ sent GET / straight
        // to the socket (curl 8.18.0 on Linux; the Windows 8.21.0 build ignored an unparsable -x too).
        var connection = new ScriptedConnection([]);
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => connection };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new UnixSocketAddress("/tmp/l.sock", IsAbstract: false));
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 9, null) };

        var result = await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("bytes written", 0, connection.Written.Count);
        Assert.AreSame(connection, result.Connection);
        Assert.IsEmpty(connection.Written);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAUnixSocketAndTls_RunsTheHandshakeToTheUrlsHost()
    {
        var events = new RecordingTransferEvents();
        var tls = new FakeTlsProvider();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() };
        var connector = CreateUnixSocketConnector(new FakeDnsResolver(), dialer, tls, new UnixSocketAddress("/run/app.sock", IsAbstract: false));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { Events = events });

        Diagnostics.Assert("TLS target host", "example.com", tls.ReceivedTargetHost);
        Assert.AreSame(tls.SecuredConnection, result.Connection);
        Assert.AreEqual("example.com", tls.ReceivedTargetHost);
        Assert.IsNotNull(result.Timings!.TlsHandshakeCompleted);
        Assert.AreEqual("/run/app.sock", events.Opened.Single().HostName);
        Assert.AreEqual("/run/app.sock", result.UnixSocketPath);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheUnixSocketDialStallsPastTheConnectTimeout_FailsWithExit28()
    {
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.Advance(1001) };
        var connector = new TcpConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond, unixSocket: new UnixSocketAddress("s.sock", IsAbstract: false));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false));

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1001 milliseconds", result.ErrorMessage);
    }

    [TestMethod]
    public void UnixSocket_IsTheConstructorsSocket()
    {
        var socket = new UnixSocketAddress("/run/app.sock", IsAbstract: false);

        Diagnostics.Arrange("socket", socket);
        Diagnostics.Act("connector's unix socket", CreateUnixSocketConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), socket).UnixSocket);
        Diagnostics.Assert("connector's unix socket", socket, CreateUnixSocketConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), socket).UnixSocket);
        Assert.AreSame(socket, CreateUnixSocketConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), socket).UnixSocket);
        Assert.IsNull(CreateConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider()).UnixSocket);
    }

    private static TcpConnector CreateUnixSocketConnector(IDnsResolver resolver, ITcpDialer dialer, ITlsProvider tls, UnixSocketAddress socket) =>
        new(resolver, dialer, tls, new ManualTimeProvider(), unixSocket: socket);
}
