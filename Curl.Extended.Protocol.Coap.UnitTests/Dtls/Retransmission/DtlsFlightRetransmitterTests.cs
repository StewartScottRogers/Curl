using System.Collections.Concurrent;
using Curl.Extended.Protocol.Coap.Client;

namespace Curl.Extended.Protocol.Coap.Dtls.Retransmission;

[TestClass]
public sealed class DtlsFlightRetransmitterTests
{
    private static readonly ReadOnlyMemory<byte>[] Flight = [new byte[] { 1, 2 }, new byte[] { 3 }];

    [TestMethod]
    public async Task SendFlightAsync_Rfc6347Section4_2_4_ResendsAfter1Then2Then4SecondsCappedAt60AndGivesUpAfterTheLimit()
    {
        ImmediateTimeProvider clock = new();
        ConcurrentQueue<byte[]> sent = new();
        DtlsFlightRetransmitter retransmitter = new(Recording(sent), clock);

        await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
            await retransmitter.SendFlightAsync(Flight, Never<int>, CancellationToken.None));

        CollectionAssert.AreEqual(
            new[] { 1, 2, 4, 8, 16, 32, 60, 60, 60, 60, 60, 60, 60 }.Select(s => TimeSpan.FromSeconds(s)).ToArray(),
            clock.DueTimes.ToArray());
        Assert.AreEqual((DtlsFlightRetransmitter.MaximumRetransmissions + 1) * Flight.Length, sent.Count);
    }

    [TestMethod]
    public async Task SendFlightAsync_Rfc6347Section4_2_4_SendsEachDatagramOfTheFlightInOrderEachTime()
    {
        ConcurrentQueue<byte[]> sent = new();
        DtlsFlightRetransmitter retransmitter = new(Recording(sent), new ImmediateTimeProvider());

        await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
            await retransmitter.SendFlightAsync(Flight, Never<int>, CancellationToken.None));

        byte[][] datagrams = [.. sent];
        for (int index = 0; index < datagrams.Length; index += 2)
        {
            CollectionAssert.AreEqual(new byte[] { 1, 2 }, datagrams[index]);
            CollectionAssert.AreEqual(new byte[] { 3 }, datagrams[index + 1]);
        }
    }

    [TestMethod]
    public async Task SendFlightAsync_Rfc6347Section4_2_4_StopsResendingOnceThePeersNextFlightStarts()
    {
        ImmediateTimeProvider clock = new();
        ConcurrentQueue<byte[]> sent = new();
        TaskCompletionSource<string> reply = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DtlsFlightRetransmitter retransmitter = new(
            (datagram, _) =>
            {
                sent.Enqueue(datagram.ToArray());
                if (sent.Count == 2 * Flight.Length)
                {
                    reply.TrySetResult("ServerHello");
                }

                return ValueTask.CompletedTask;
            },
            clock);

        string answer = await retransmitter.SendFlightAsync(Flight, _ => reply.Task, CancellationToken.None);

        Assert.AreEqual("ServerHello", answer);
        Assert.HasCount(2 * Flight.Length, sent);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, clock.DueTimes.ToArray());
    }

    [TestMethod]
    public async Task SendFlightAsync_ReplyBeforeTheFirstTimeout_SendsTheFlightOnce()
    {
        ConcurrentQueue<byte[]> sent = new();
        DtlsFlightRetransmitter retransmitter = new(Recording(sent), new ImmediateTimeProvider());

        int answer = await retransmitter.SendFlightAsync(Flight, _ => Task.FromResult(7), CancellationToken.None);

        Assert.AreEqual(7, answer);
        Assert.HasCount(Flight.Length, sent);
    }

    [TestMethod]
    public async Task SendFlightAsync_Cancelled_ThrowsOperationCanceledAndCancelsTheWaitForTheReply()
    {
        using CancellationTokenSource cancel = new();
        CancellationToken replyToken = default;
        DtlsFlightRetransmitter retransmitter = new(
            (_, _) =>
            {
                cancel.Cancel();
                return ValueTask.CompletedTask;
            },
            TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await retransmitter.SendFlightAsync(
                Flight,
                token =>
                {
                    replyToken = token;
                    return Never<int>(token);
                },
                cancel.Token));

        Assert.IsTrue(replyToken.IsCancellationRequested);
    }

    [TestMethod]
    public void NextTimeout_Rfc6347Section4_2_4_1_DoublesUpTo60Seconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(2), DtlsFlightRetransmitter.NextTimeout(TimeSpan.FromSeconds(1)));
        Assert.AreEqual(TimeSpan.FromSeconds(60), DtlsFlightRetransmitter.NextTimeout(TimeSpan.FromSeconds(30)));
        Assert.AreEqual(TimeSpan.FromSeconds(60), DtlsFlightRetransmitter.NextTimeout(TimeSpan.FromSeconds(32)));
        Assert.AreEqual(TimeSpan.FromSeconds(60), DtlsFlightRetransmitter.NextTimeout(TimeSpan.FromSeconds(60)));
    }

    private static Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> Recording(ConcurrentQueue<byte[]> sent) =>
        (datagram, _) =>
        {
            sent.Enqueue(datagram.ToArray());
            return ValueTask.CompletedTask;
        };

    private static Task<T> Never<T>(CancellationToken cancellationToken) =>
        Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<T>(_ => throw new OperationCanceledException(cancellationToken), TaskScheduler.Default);
}
