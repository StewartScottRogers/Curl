using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>
/// Checks <see cref="NamedPipeConnector" /> against Microsoft's "Named Pipes": a client opens the pipe by its name and,
/// in byte mode, reads and writes a byte stream with no framing of its own.
/// </summary>
[TestClass]
public sealed class NamedPipeConnectorTests
{
    private static readonly NamedPipeName Pipe = new(".", "docker_engine");

    [TestMethod]
    public async Task ConnectAsync_ByteMode_CarriesTheRequestAndResponseBytesUnchanged()
    {
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        FakeNamedPipeOpener opener = new() { ServerBytes = response };
        byte[] request = Encoding.ASCII.GetBytes("GET /_ping HTTP/1.1\r\nHost: localhost\r\n\r\n");

        ConnectResult result = await new NamedPipeConnector(Pipe, opener).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await using IConnection connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        byte[] read = new byte[response.Length + 8];
        int count = await connection.ReadAsync(read, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(request, opener.LastPipe!.Written.ToArray());
        Assert.AreEqual(1, opener.LastPipe.FlushCount);
        CollectionAssert.AreEqual(response, read[..count]);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_AnyHostAndPort_OpensTheOnePipe()
    {
        FakeNamedPipeOpener opener = new();
        NamedPipeConnector connector = new(Pipe, opener);

        await (await connector.ConnectAsync(new ConnectTarget("example.com", 8080, UseTls: false), CancellationToken.None)).Connection!.DisposeAsync();
        await (await connector.ConnectAsync(new ConnectTarget("other.test", 80, UseTls: false), CancellationToken.None)).Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { Pipe, Pipe }, opener.Opened);
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesThePipe()
    {
        FakeNamedPipeOpener opener = new();

        ConnectResult result = await new NamedPipeConnector(Pipe, opener).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.IsTrue(opener.LastPipe!.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTarget_IsRefusedAsNotBuiltInWithoutOpeningThePipe()
    {
        FakeNamedPipeOpener opener = new();

        ConnectResult result = await new NamedPipeConnector(Pipe, opener).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual(@"TLS over the named pipe \\.\pipe\docker_engine is not built in", result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    [DataRow(typeof(IOException), DisplayName = "not there or busy")]
    [DataRow(typeof(TimeoutException), DisplayName = "timed out")]
    [DataRow(typeof(UnauthorizedAccessException), DisplayName = "access denied")]
    public async Task ConnectAsync_PipeThatCannotBeOpened_FailsAsCouldNotConnect(Type failureType)
    {
        Exception failure = (Exception)Activator.CreateInstance(failureType, "the reason")!;

        ConnectResult result = await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(failure)).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(@"Failed to connect to the named pipe \\.\pipe\docker_engine: the reason", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_Cancelled_LetsTheCancellationThrough() =>
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener(new OperationCanceledException())).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None));

    [TestMethod]
    public async Task ConnectAsync_NullTarget_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await new NamedPipeConnector(Pipe, new FakeNamedPipeOpener()).ConnectAsync(null!, CancellationToken.None));
}
