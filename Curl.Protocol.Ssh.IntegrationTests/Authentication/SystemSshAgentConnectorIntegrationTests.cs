using System.IO.Pipes;
using System.Net.Sockets;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Connects <see cref="SystemSshAgentConnector" /> to a real agent endpoint served here, as
/// libssh2 1.11.1 does (ADR-0271): a named pipe on Windows and a Unix domain socket in the
/// temporary folder, and checks bytes cross it. Where it looks is pinned by
/// <c>SystemSshAgentConnectorTests</c> in Curl.Protocol.Ssh.UnitTests.
/// </summary>
[TestClass]
public sealed class SystemSshAgentConnectorIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
