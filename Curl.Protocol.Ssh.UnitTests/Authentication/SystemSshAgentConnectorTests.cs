using System.IO.Pipes;
using System.Net.Sockets;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins where <see cref="SystemSshAgentConnector" /> looks for the agent, as libssh2 1.11.1
/// does (ADR-0271): a pipe on Windows, <c>SSH_AUTH_SOCK</c>'s Unix socket elsewhere. The
/// connections themselves run in the Integration tests, against a pipe and a socket served
/// here.
/// </summary>
[TestClass]
public sealed class SystemSshAgentConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(@"\\.\pipe\openssh-ssh-agent", ".", "openssh-ssh-agent", DisplayName = "Win32-OpenSSH's agent")]
    [DataRow(@"\\host\PIPE\agent", "host", "agent", DisplayName = "another server, any letter case")]
    public void PipeNameOf_PipePath_SplitsTheServerAndName(string path, string server, string name)
    {
        Diagnostics.Arrange("path", path);

        (string Server, string Name)? pipe = SystemSshAgentConnector.PipeNameOf(path);

        Diagnostics.Act("pipe", pipe?.ToString() ?? "null");
        Diagnostics.Assert("pipe", (server, name).ToString(), pipe?.ToString() ?? "null");
        Assert.AreEqual((server, name), pipe);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("/tmp/ssh-XXXX/agent.1", DisplayName = "a Unix socket path")]
    [DataRow(@"C:\Users\me\agent", DisplayName = "a file path")]
    [DataRow(@"\\.\pipe\", DisplayName = "no pipe name")]
    [DataRow(@"\\.\share\agent", DisplayName = "not the pipe folder")]
    [DataRow(@"\\\pipe\agent", DisplayName = "no server")]
    [DataRow(@"\\.\pipe\a\b", DisplayName = "a pipe name with a backslash")]
    [DataRow(@"x\\.\pipe\agent", DisplayName = "text before the server")]
    [DataRow(@"\x\.\pipe\agent", DisplayName = "one leading backslash")]
    public void PipeNameOf_NoPipePath_ReturnsNull(string path)
    {
        Diagnostics.Arrange("path", path);

        (string Server, string Name)? pipe = SystemSshAgentConnector.PipeNameOf(path);

        Diagnostics.Act("pipe", pipe?.ToString() ?? "null");
        Diagnostics.Assert("pipe", "null", pipe?.ToString() ?? "null");
        Assert.IsNull(pipe);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "SSH_AUTH_SOCK unset")]
    [DataRow("", DisplayName = "SSH_AUTH_SOCK empty")]
    public async Task ConnectAsync_UnixWithoutASocketPath_FindsNoAgent(string? authSocket)
    {
        SystemSshAgentConnector connector = new(_ => authSocket, isWindows: false);
        Diagnostics.Arrange("SSH_AUTH_SOCK", authSocket ?? "(unset)");

        Assert.IsNull(await ConnectExpectingNoAgentAsync(connector));
    }

    [TestMethod]
    public async Task ConnectAsync_UnixSocketNotThere_FindsNoAgent()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bl902-{Guid.NewGuid():N}.sock");
        SystemSshAgentConnector connector = new(_ => path, isWindows: false);
        Diagnostics.Arrange("SSH_AUTH_SOCK", "a socket path in the temporary folder that does not exist");

        Assert.IsNull(await ConnectExpectingNoAgentAsync(connector));
    }

    [TestMethod]
    public async Task ConnectAsync_WindowsPathNamingNoPipe_FindsNoAgent()
    {
        SystemSshAgentConnector connector = new(_ => "/tmp/agent.sock", isWindows: true);
        Diagnostics.Arrange("SSH_AUTH_SOCK on Windows", "/tmp/agent.sock");

        Assert.IsNull(await ConnectExpectingNoAgentAsync(connector));
    }

    [TestMethod]
    public async Task ConnectAsync_WindowsPipeNotThere_FindsNoAgent()
    {
        SystemSshAgentConnector connector = new(_ => null, isWindows: true, windowsDefaultPipe: $@"\\.\pipe\bl902-{Guid.NewGuid():N}");
        Diagnostics.Arrange("default pipe on Windows", "a pipe name nobody serves");

        Assert.IsNull(await ConnectExpectingNoAgentAsync(connector));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(true, DisplayName = "named by SSH_AUTH_SOCK")]
    [DataRow(false, DisplayName = "the default, SSH_AUTH_SOCK unset")]
    public async Task ConnectAsync_WindowsPipeServed_ConnectsToIt(bool named)
    {
        string name = $"bl902-{Guid.NewGuid():N}";
        await using NamedPipeServerStream server = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task accepted = server.WaitForConnectionAsync();
        string path = $@"\\.\pipe\{name}";
        SystemSshAgentConnector connector = named ? new(_ => path, isWindows: true) : new(_ => null, isWindows: true, windowsDefaultPipe: path);
        Diagnostics.Arrange("pipe", named ? "named by SSH_AUTH_SOCK" : "the default");

        Stream? connection = await connector.ConnectAsync(CancellationToken.None);

        await accepted;
        Diagnostics.Act("connected", connection is not null);
        Diagnostics.Assert("connected", true, connection is not null);
        Assert.IsNotNull(connection);
        await using (connection)
        {
            await AssertCarriesBytesAsync(connection, server);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_UnixSocketServed_ConnectsToIt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bl902-{Guid.NewGuid():N}.sock");
        using Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen();
        try
        {
            Task<Socket> accepted = listener.AcceptAsync();
            SystemSshAgentConnector connector = new(_ => path, isWindows: false);
            Diagnostics.Arrange("SSH_AUTH_SOCK", "a Unix socket served here");

            Stream? connection = await connector.ConnectAsync(CancellationToken.None);

            using Socket peer = await accepted;
            Diagnostics.Act("connected", connection is not null);
            Diagnostics.Assert("connected", true, connection is not null);
            Assert.IsNotNull(connection);
            await using (connection)
            {
                await AssertCarriesBytesAsync(connection, new NetworkStream(peer));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Connects where no agent is, writing what it got.
    private async Task<Stream?> ConnectExpectingNoAgentAsync(SystemSshAgentConnector connector)
    {
        Stream? connection = await connector.ConnectAsync(CancellationToken.None);
        Diagnostics.Act("connection", connection is null ? "none" : connection.GetType().Name);
        Diagnostics.Assert("connection", "none", connection is null ? "none" : connection.GetType().Name);
        return connection;
    }

    // The read starts before the write is awaited: the test's pipe has no buffer, so a
    // write to it completes only once the peer reads, and awaiting it first hangs.
    private static async Task AssertCarriesBytesAsync(Stream connection, Stream peer)
    {
        byte[] received = new byte[5];
        Task read = peer.ReadExactlyAsync(received).AsTask();
        await connection.WriteAsync(new byte[] { 0, 0, 0, 1, 11 });
        await read;
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 11 }, received);
    }
}
