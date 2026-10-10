using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <see cref="InProcessCurl.RunAsync" /> runs a command line in process over the given
/// connectors and returns curl's exit code, for tools outside the test projects (BL-1750).
/// </summary>
[TestClass]
public sealed class InProcessCurlTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_FileUrl_WritesTheFileToStandardOutputAndReturnsZero()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl1750-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "a.txt");
        File.WriteAllBytes(path, "hello\n"u8.ToArray());
        string url = new Uri(path).AbsoluteUri;
        Diagnostics.Arrange("url", "file:///<temporary>/a.txt containing hello and a line feed");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, "unused");

        int exitCode;
        try
        {
            using (Diagnostics.Phase("run"))
            {
                exitCode = await InProcessCurl.RunAsync(["-s", url], standardOutput, standardError, standardInput, new RefusingConnector(), datagramConnector);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual("hello\n"u8.ToArray(), standardOutput.ToArray());
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_MissingFile_ReturnsExit37()
    {
        string url = new Uri(Path.Combine(Path.GetTempPath(), "curl-bl1750-" + Guid.NewGuid().ToString("N"), "missing.txt")).AbsoluteUri;
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await InProcessCurl.RunAsync(["-s", url], standardOutput, standardError, new MemoryStream(), new RefusingConnector(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"));

        Diagnostics.Assert("exit code", 37, exitCode);
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    public void RunAsync_NullArgument_Throws()
    {
        MemoryStream stream = new();
        RefusingConnector connector = new();
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, "unused");

        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync(null!, stream, stream, stream, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], null!, stream, stream, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, null!, stream, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, null!, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, null!, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, connector, null!));
    }

    [TestMethod]
    public async Task RunAsync_DialerProxyTunnel_SendsConnectThroughTheTcpConnector()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 Connection established\r\n\r\n"u8.ToArray(), "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"u8.ToArray()]);

        (int exitCode, _, _) = await RunOverDialerAsync(["-s", "-p", "-x", "http://proxy.example:3128", "http://origin.example/"], server, new LoopbackDnsResolver());

        string sent = Encoding.ASCII.GetString(server.Written);
        Diagnostics.Bytes("sent", server.Written);
        Diagnostics.Assert("first line", "CONNECT origin.example:80 HTTP/1.1", sent.Split("\r\n")[0]);
        Assert.StartsWith("CONNECT origin.example:80 HTTP/1.1\r\n", sent);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_DialerHaproxyProtocol_SendsTheProxyLineFirst()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        (int exitCode, _, _) = await RunOverDialerAsync(["-s", "-b", "a=b", "--haproxy-protocol", "http://origin.example/"], server, new LoopbackDnsResolver());

        string sent = Encoding.ASCII.GetString(server.Written);
        Diagnostics.Bytes("sent", server.Written);
        Assert.StartsWith("PROXY ", sent);
        Assert.Contains("\r\nGET / HTTP/1.1\r\n", sent);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_DialerOnionHost_ReturnsExit6WithTheRfc7686Message()
    {
        ScriptedConnector server = new([]);

        (int exitCode, _, string standardError) = await RunOverDialerAsync(["-sS", "http://x.onion/"], server, new LoopbackDnsResolver());

        Diagnostics.Assert("exit code", 6, exitCode);
        Assert.AreEqual(6, exitCode);
        Assert.Contains("curl: (6) Not resolving .onion address (RFC 7686)", standardError);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_DialerUnresolvableName_ReturnsExit6()
    {
        ScriptedConnector server = new([]);

        (int exitCode, _, string standardError) = await RunOverDialerAsync(["-sS", "http://nowhere.example/"], server, new UnresolvingDnsResolver());

        Diagnostics.Assert("exit code", 6, exitCode);
        Assert.AreEqual(6, exitCode);
        Assert.Contains("nowhere.example", standardError);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_DialerMalformedResolveEntry_ReturnsExit49()
    {
        ScriptedConnector server = new([]);

        (int exitCode, _, _) = await RunOverDialerAsync(["-s", "--resolve", "bogus", "http://origin.example/"], server, new LoopbackDnsResolver());

        Diagnostics.Assert("exit code", 49, exitCode);
        Assert.AreEqual(49, exitCode);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public void RunAsync_DialerNullArgument_Throws()
    {
        MemoryStream stream = new();
        ScriptedTcpDialer dialer = new(new ScriptedConnector([]));
        LoopbackDnsResolver resolver = new();
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, "unused");

        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync(null!, stream, stream, stream, dialer, resolver, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], null!, stream, stream, dialer, resolver, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, null!, stream, dialer, resolver, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, null!, dialer, resolver, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, null!, resolver, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, dialer, null!, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, dialer, resolver, null!));
    }

    [TestMethod]
    public async Task RunAsync_EnvironmentNoProxyNamesTheHost_SendsTheRequestPastTheProxy()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);
        Dictionary<string, string> environment = new() { ["NO_PROXY"] = "origin.example" };

        (int exitCode, _, _) = await RunOverDialerAsync(["-s", "-x", "http://proxy.example:3128", "http://origin.example/1249"], server, new LoopbackDnsResolver(), environment);

        string sent = Encoding.ASCII.GetString(server.Written);
        Diagnostics.Bytes("sent", server.Written);
        Assert.StartsWith("GET /1249 HTTP/1.1\r\n", sent);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_EnvironmentXdgConfigHomeHoldsCurlrc_ReadsItsOptions()
    {
        string directory = CreateTemporaryDirectory();
        File.WriteAllText(Path.Combine(directory, "curlrc"), "-d moo\n");
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        string sent;
        try
        {
            (int exitCode, _, _) = await RunOverDialerAsync(["-s", "http://origin.example/433"], server, new LoopbackDnsResolver(), new() { ["XDG_CONFIG_HOME"] = directory });
            sent = Encoding.ASCII.GetString(server.Written);
            Assert.AreEqual(0, exitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Diagnostics.Bytes("sent", server.Written);
        Assert.StartsWith("POST /433 HTTP/1.1\r\n", sent);
        Assert.EndsWith("\r\n\r\nmoo", sent);
    }

    [TestMethod]
    public async Task RunAsync_EnvironmentCurlHomeHoldsDotCurlrc_ReadsItsOptions()
    {
        string directory = CreateTemporaryDirectory();
        File.WriteAllText(Path.Combine(directory, ".curlrc"), "-d moo\n");
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        string sent;
        try
        {
            (int exitCode, _, _) = await RunOverDialerAsync(["-s", "http://origin.example/436"], server, new LoopbackDnsResolver(), new() { ["CURL_HOME"] = directory });
            sent = Encoding.ASCII.GetString(server.Written);
            Assert.AreEqual(0, exitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Diagnostics.Bytes("sent", server.Written);
        Assert.StartsWith("POST /436 HTTP/1.1\r\n", sent);
    }

    [TestMethod]
    public async Task RunAsync_EnvironmentHomeHoldsIpfsGateway_SendsTheRequestToTheGateway()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".ipfs"));
        File.WriteAllText(Path.Combine(directory, ".ipfs", "gateway"), "http://gateway.example\n");
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        string sent;
        try
        {
            (int exitCode, _, _) = await RunOverDialerAsync(["-s", "ipfs://bafybeidecnvkrygux6uoukouzps5ofkeevoqland7kopseiod6pzqvjg7u"], server, new LoopbackDnsResolver(), new() { ["HOME"] = directory });
            sent = Encoding.ASCII.GetString(server.Written);
            Assert.AreEqual(0, exitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Diagnostics.Bytes("sent", server.Written);
        Assert.StartsWith("GET /ipfs/bafybeidecnvkrygux6uoukouzps5ofkeevoqland7kopseiod6pzqvjg7u HTTP/1.1\r\n", sent);
    }

    [TestMethod]
    public async Task RunAsync_EnvironmentHomeHoldsMalformedIpfsGateway_ReturnsExit3()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".ipfs"));
        File.WriteAllText(Path.Combine(directory, ".ipfs", "gateway"), "http://gateway.example/?query=1\n");
        ScriptedConnector server = new([]);

        int exitCode;
        try
        {
            (exitCode, _, _) = await RunOverDialerAsync(["-s", "ipfs://bafybeidecnvkrygux6uoukouzps5ofkeevoqland7kopseiod6pzqvjg7u"], server, new LoopbackDnsResolver(), new() { ["HOME"] = directory });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public void RunAsync_DialerNullEnvironment_Throws()
    {
        MemoryStream stream = new();
        ScriptedTcpDialer dialer = new(new ScriptedConnector([]));
        LoopbackDnsResolver resolver = new();
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, "unused");
        Func<string, string?> environment = _ => null;

        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync(null!, stream, stream, stream, dialer, resolver, datagramConnector, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], null!, stream, stream, dialer, resolver, datagramConnector, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, null!, stream, dialer, resolver, datagramConnector, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, null!, dialer, resolver, datagramConnector, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, null!, resolver, datagramConnector, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, dialer, null!, datagramConnector, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, dialer, resolver, null!, environment));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, dialer, resolver, datagramConnector, null!));
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl1977-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<(int ExitCode, byte[] StandardOutput, string StandardError)> RunOverDialerAsync(string[] arguments, ScriptedConnector server, IDnsResolver resolver, Dictionary<string, string> environment)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        int exitCode = await InProcessCurl.RunAsync(arguments, standardOutput, standardError, new MemoryStream(), new ScriptedTcpDialer(server), resolver, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), name => environment.GetValueOrDefault(name));
        return (exitCode, standardOutput.ToArray(), Encoding.UTF8.GetString(standardError.ToArray()));
    }

    private static async Task<(int ExitCode, byte[] StandardOutput, string StandardError)> RunOverDialerAsync(string[] arguments, ScriptedConnector server, IDnsResolver resolver)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        int exitCode = await InProcessCurl.RunAsync(arguments, standardOutput, standardError, new MemoryStream(), new ScriptedTcpDialer(server), resolver, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"));
        return (exitCode, standardOutput.ToArray(), Encoding.UTF8.GetString(standardError.ToArray()));
    }

    /// <summary>A resolver that finds no address for any name.</summary>
    private sealed class UnresolvingDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<System.Net.IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<System.Net.IPAddress>>([]);
    }

    /// <summary>A connector that refuses every connect with exit 7; a <c>file://</c> transfer never calls it.</summary>
    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
    }
}
