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

    private static TransferContext ContextOf(TimeProvider clock, CancellationToken cancellationToken = default) =>
        new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream(), TimeProvider = clock, CancellationToken = cancellationToken };

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
