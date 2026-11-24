using System.Net;
using System.Net.Sockets;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Tcp;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tcp;

/// <summary>
/// Checks how a TCP query whose connection fails or closes ends: curl's exit code and words, never .NET's exception
/// text.
/// </summary>
[TestClass]
public sealed class DnsTcpTransportFailureTests
{
    private static readonly byte[] Query = [0x12, 0x34];

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExchangeAsync_ResetWhileReadingOnWindows_FailsWithExit56AndCurlsRecvFailure()
    {
        DnsQueryException failure = await FailureOfAsync(new FailingConnection { ReadFailure = Reset() });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", failure.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExchangeAsync_ResetWhileReadingOffWindows_FailsWithExit56AndTheSocketErrorsWords()
    {
        IOException reset = Reset();

        DnsQueryException failure = await FailureOfAsync(new FailingConnection { ReadFailure = reset });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual($"Recv failure: {reset.InnerException!.Message}", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_ReadFailsWithoutASocketError_FailsWithExit56AndCurlsGeneralWords()
    {
        DnsQueryException failure = await FailureOfAsync(new FailingConnection { ReadFailure = new IOException(".NET words") });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_TlsEndsWithoutCloseNotify_FailsWithExit56AndTheTlsLayersWords()
    {
        DnsQueryException failure = await FailureOfAsync(new FailingConnection { ReadFailure = new MissingCloseNotifyException("missing close_notify") });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("missing close_notify", failure.Message);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(4)]
    public async Task ExchangeAsync_ClosedBeforeTheWholeLengthPrefixedAnswer_FailsWithExit56(int octetsSent)
    {
        byte[] shortReply = [0, 8, 0x12, 0x34];

        DnsQueryException failure = await FailureOfAsync(new FailingConnection { Reply = shortReply[..octetsSent] });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("The DNS server closed the connection before its whole answer arrived", failure.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExchangeAsync_ResetWhileSendingOnWindows_FailsWithExit55AndCurlsSendFailure()
    {
        DnsQueryException failure = await FailureOfAsync(new FailingConnection { WriteFailure = Reset() });

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", failure.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExchangeAsync_ResetWhileSendingOffWindows_FailsWithExit55AndTheSocketErrorsWords()
    {
        IOException reset = Reset();

        DnsQueryException failure = await FailureOfAsync(new FailingConnection { WriteFailure = reset });

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual($"Send failure: {reset.InnerException!.Message}", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_SendFailsWithoutASocketError_FailsWithExit55AndCurlsGeneralWords()
    {
        DnsQueryException failure = await FailureOfAsync(new FailingConnection { WriteFailure = new IOException(".NET words") });

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", failure.Message);
    }

    private static IOException Reset() =>
        new("Unable to read data from the transport connection.", new SocketException((int)SocketError.ConnectionReset));

    private static async Task<DnsQueryException> FailureOfAsync(FailingConnection connection)
    {
        var context = new TransferContext { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream() };

        return await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsTcpTransport(connection).ExchangeAsync("h", 53, Query, context));
    }

    // A connector giving itself as the connection: it fails the write or read it is told to, and otherwise reads its
    // reply and then the end of the stream.
    private sealed class FailingConnection : IConnector, IConnection
    {
        private int position;

        public IOException? WriteFailure { get; init; }

        public IOException? ReadFailure { get; init; }

        public byte[] Reply { get; init; } = [];

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(this));

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (ReadFailure is not null)
            {
                return ValueTask.FromException<int>(ReadFailure);
            }

            int length = Math.Min(buffer.Length, Reply.Length - position);
            Reply.AsMemory(position, length).CopyTo(buffer);
            position += length;
            return ValueTask.FromResult(length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            WriteFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(WriteFailure);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
