using System.Net;
using System.Net.Sockets;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Udp;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport;

/// <summary>Checks the query over UDP and the retry over TCP of a truncated answer (RFC 1035 section 4.2.1).</summary>
[TestClass]
public sealed class DnsUdpTransportTests
{
    private static readonly byte[] Query = new DnsWire().Header(0x1234, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();
    private static readonly byte[] Answer = DnsWire.SingleAnswer(1, [192, 0, 2, 1]);

    [TestMethod]
    public async Task ExchangeAsync_AnsweredAtOnce_GivesTheAnswerFromOneDatagram()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);

        byte[] answer = await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("192.0.2.53", 53, Query, ContextOf(new ImmediateTimeProvider()));

        CollectionAssert.AreEqual(Answer, answer);
        Assert.HasCount(1, server.Datagrams);
        CollectionAssert.AreEqual(Query, server.Datagrams[0]);
        Assert.AreEqual(("udp", "192.0.2.53", 53), server.Opened.Single());
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    public async Task ExchangeAsync_DatagramsThatAreNotTheAnswer_AreIgnored()
    {
        byte[] otherId = [.. Answer];
        otherId[1] ^= 0xFF;
        var server = new ScriptedDnsServer((_, _) => [[0x12, 0x34, 0x81], otherId, Query, Answer], []);

        byte[] answer = await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider()));

        CollectionAssert.AreEqual(Answer, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_ResponsesWithAnotherQuestion_AreIgnoredAndTheMatchingOneTaken()
    {
        // RFC 5452 section 9.1: only a response whose question is the query's is accepted.
        byte[] otherName = ResponseWithQuestion("example.net", 1, 1);
        byte[] otherType = ResponseWithQuestion("example.com", 28, 1);
        byte[] otherClass = ResponseWithQuestion("example.com", 1, 3);
        byte[] cutShort = [.. Answer[..20]];
        var server = new ScriptedDnsServer((_, _) => [otherName, otherType, otherClass, cutShort, Answer], []);

        byte[] answer = await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider()));

        CollectionAssert.AreEqual(Answer, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_ResponseWhoseQuestionDiffersOnlyInCase_IsTaken()
    {
        // RFC 5452 section 9.1: QNAME is compared without regard to ASCII case.
        byte[] mixedCase = ResponseWithQuestion("ExAmPlE.CoM", 1, 1);
        var server = new ScriptedDnsServer((_, _) => [mixedCase], []);

        byte[] answer = await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider()));

        CollectionAssert.AreEqual(mixedCase, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_ResponseWithNoQuestionSection_IsTaken()
    {
        // ADR-1225: as dig, a response with no question (a FORMERR, say) is checked by its ID alone.
        byte[] noQuestion = new DnsWire().Header(0x1234, 0x8181, 0, 0, 0, 0).ToArray();
        var server = new ScriptedDnsServer((_, _) => [noQuestion], []);

        byte[] answer = await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider()));

        CollectionAssert.AreEqual(noQuestion, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_FirstQueryUnanswered_SendsItAgain()
    {
        var server = new ScriptedDnsServer((send, _) => send == 2 ? [Answer] : [], []);

        byte[] answer = await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider()));

        CollectionAssert.AreEqual(Answer, answer);
        Assert.HasCount(2, server.Datagrams);
    }

    [TestMethod]
    public async Task ExchangeAsync_NoAnswerToAnyAttempt_FailsWithExit28AfterThreeQueriesOfFiveSecondsEach()
    {
        var server = new ScriptedDnsServer((_, _) => [], []);
        var clock = new ImmediateTimeProvider();

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(clock)));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.HasCount(DnsUdpTransport.Attempts, server.Datagrams);
        CollectionAssert.AreEqual(Enumerable.Repeat(TimeSpan.FromSeconds(5), 3).ToArray(), clock.DueTimes.ToArray());
    }

    [TestMethod]
    public async Task ExchangeAsync_TruncatedAnswer_AsksAgainOverTheFallback()
    {
        byte[] truncated = [.. Answer];
        truncated[2] |= 0x02;
        var server = new ScriptedDnsServer((_, _) => [truncated], []);
        var fallback = new RecordingTransport();

        byte[] answer = await new DnsUdpTransport(server, fallback).ExchangeAsync("h", 5353, Query, ContextOf(new ImmediateTimeProvider()));

        Assert.AreSame(RecordingTransport.Answer, answer);
        Assert.AreEqual(("h", 5353), fallback.Asked.Single());
    }

    [TestMethod]
    public async Task ExchangeAsync_ChannelCannotOpen_FailsWithTheConnectorsExitCode()
    {
        var server = new ScriptedDnsServer((_, _) => [], []) { DatagramFailure = DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h") };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider())));

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, failure.ExitCode);
        Assert.AreEqual("Could not resolve host: h", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_TransferCancelled_LetsTheCancellationThrough()
    {
        var server = new ScriptedDnsServer((_, _) => [], []);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new DnsUdpTransport(server, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(TimeProvider.System, cancellation.Token)));
    }

    [TestMethod]
    public async Task ExchangeAsync_ChannelOpened_ReportsTheConnectionForTheVerboseEstablishedLine()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);
        var connector = new EndPointOverridingConnector(server, null, null, 7);
        var events = new ConnectionOpenedCapturingTransferEvents(NoTransferEvents.Instance);

        await new DnsUdpTransport(connector, new RecordingTransport()).ExchangeAsync("localhost", 53, Query, ContextOf(new ImmediateTimeProvider(), events: events));

        ConnectionOpenedEvent opened = events.Opened!;
        Assert.AreEqual("localhost", opened.HostName);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 53), opened.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Any, 0), opened.LocalEndPoint);
        Assert.AreEqual(7, opened.ConnectionNumber);
    }

    [TestMethod]
    public async Task ExchangeAsync_ChannelNamesItsLocalAddress_ReportsThatLocalAddress()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);
        var local = new IPEndPoint(IPAddress.Loopback, 50000);
        var events = new ConnectionOpenedCapturingTransferEvents(NoTransferEvents.Instance);

        await new DnsUdpTransport(new EndPointOverridingConnector(server, null, local, 1), new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider(), events: events));

        Assert.AreEqual(local, events.Opened!.LocalEndPoint);
    }

    [TestMethod]
    public async Task ExchangeAsync_ServerEndPointIsNotAnAddress_ReportsNoConnection()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);
        var connector = new EndPointOverridingConnector(server, new DnsEndPoint("h", 53), null, 1);
        var events = new ConnectionOpenedCapturingTransferEvents(NoTransferEvents.Instance);

        byte[] answer = await new DnsUdpTransport(connector, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider(), events: events));

        CollectionAssert.AreEqual(Answer, answer);
        Assert.IsNull(events.Opened);
    }

    private static byte[] ResponseWithQuestion(string name, int type, int recordClass) =>
        new DnsWire().Header(0x1234, 0x8180, 1, 0, 0, 0).Question(DnsWire.Name(name), type, recordClass).ToArray();

    private static TransferContext ContextOf(TimeProvider clock, CancellationToken cancellationToken = default, ITransferEvents? events = null) =>
        new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream(), TimeProvider = clock, CancellationToken = cancellationToken, Events = events ?? NoTransferEvents.Instance };

    [TestMethod]
    public async Task ExchangeAsync_RefusalAfterAStrayDatagram_ThrowsExitRecvError()
    {
        var connector = new RefusingConnector(failSend: false, new SocketException((int)SocketError.ConnectionReset));

        DnsQueryException exception = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsUdpTransport(connector, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider())));

        Assert.AreEqual(CurlExitCode.RecvError, exception.ExitCode);
    }

    [TestMethod]
    public async Task ExchangeAsync_SendFailsWithASocketError_ThrowsExitSendError()
    {
        var connector = new RefusingConnector(failSend: true, new SocketException((int)SocketError.ConnectionReset));

        DnsQueryException exception = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsUdpTransport(connector, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider())));

        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
    }

    [TestMethod]
    public async Task ExchangeAsync_SendFailsWithAPortUnreachableError_EscapesUnchanged()
    {
        var error = new PortUnreachable();
        var connector = new RefusingConnector(failSend: true, error);

        SocketException thrown = await Assert.ThrowsExactlyAsync<PortUnreachable>(async () =>
            await new DnsUdpTransport(connector, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider())));

        Assert.AreSame(error, thrown);
    }

    [TestMethod]
    public async Task ExchangeAsync_ReceiveFailsWithAPortUnreachableError_EscapesUnchanged()
    {
        var error = new PortUnreachable();
        var connector = new RefusingConnector(failSend: false, error);

        SocketException thrown = await Assert.ThrowsExactlyAsync<PortUnreachable>(async () =>
            await new DnsUdpTransport(connector, new RecordingTransport()).ExchangeAsync("h", 53, Query, ContextOf(new ImmediateTimeProvider())));

        Assert.AreSame(error, thrown);
    }

    /// <summary>Stands in for Curl.Networking.DatagramPortUnreachableException, a SocketException subclass the transport must not catch.</summary>
    private sealed class PortUnreachable : SocketException
    {
        public PortUnreachable()
            : base((int)SocketError.ConnectionReset)
        {
        }
    }

    /// <summary>Opens a channel that gives one stray datagram and then fails with a connection reset, or fails every send.</summary>
    private sealed class RefusingConnector(bool failSend, SocketException error) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Opened(new RefusingChannel(failSend, error)));

        private sealed class RefusingChannel(bool failSend, SocketException error) : IDatagramChannel
        {
            private bool strayGiven;

            public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 53);

            public EndPoint? LocalEndPoint => null;

            public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
                failSend ? throw error : ValueTask.CompletedTask;

            public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                if (strayGiven)
                {
                    throw error;
                }

                strayGiven = true;
                return ValueTask.FromResult(new DatagramReceived(3, ServerEndPoint));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
    /// <summary>Opens the inner connector's channel and gives it the server address, local address and connection number given.</summary>
    private sealed class EndPointOverridingConnector(IDatagramConnector inner, EndPoint? serverEndPoint, EndPoint? localEndPoint, long connectionNumber) : IDatagramConnector
    {
        public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            DatagramOpenResult open = await inner.OpenAsync(host, port, cancellationToken);
            return DatagramOpenResult.Opened(new OverridingChannel(open.Channel!, serverEndPoint, localEndPoint)).WithConnectionNumber(connectionNumber);
        }

        private sealed class OverridingChannel(IDatagramChannel inner, EndPoint? serverEndPoint, EndPoint? localEndPoint) : IDatagramChannel
        {
            public EndPoint ServerEndPoint => serverEndPoint ?? inner.ServerEndPoint;

            public EndPoint? LocalEndPoint => localEndPoint;

            public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
                inner.SendAsync(datagram, destination, cancellationToken);

            public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
                inner.ReceiveAsync(buffer, cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    private sealed class RecordingTransport : IDnsQueryTransport
    {
        public static readonly byte[] Answer = [1, 2, 3];

        public List<(string Host, int Port)> Asked { get; } = [];

        public int DefaultPort => 53;

        public ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context)
        {
            Asked.Add((host, port));
            return ValueTask.FromResult(Answer);
        }
    }
}
