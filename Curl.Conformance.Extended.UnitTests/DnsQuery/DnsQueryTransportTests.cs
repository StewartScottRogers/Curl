using System.Text;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery;

/// <summary>
/// RFC 1035 sections 4.2.1, 4.2.2 and 7.2 and RFC 7766 section 5: curl's <c>dns://</c> client asks a query again over
/// TCP, after the two-octet length prefix, when the UDP reply has TC set, sends a UDP query again when no answer came
/// in its wait on the transfer's clock, and gives up with exit 28 after the last attempt.
/// </summary>
[TestClass]
public sealed class DnsQueryTransportTests
{
    private const string Name = "example.com.";

    private const string Url = "dns://localhost/example.com";

    /// <summary>How long each UDP attempt waits before the query is sent again, as dig's default <c>+timeout=5</c>.</summary>
    private static readonly TimeSpan AttemptWait = TimeSpan.FromSeconds(5);

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.2.1-1")]
    [SpecRequirement("DnsQuery:RFC7766-5-1")]
    [SpecRequirement("DnsQuery:RFC1035-4.2.2-2")]
    public Task UdpReplyWithTruncationSet_IsAskedAgainOverTcpWithTheLengthPrefix() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.2.1-1", "DnsQuery:RFC7766-5-1", "DnsQuery:RFC1035-4.2.2-2"], async () =>
        {
            // The UDP reply sets QR, TC, RD and RA and carries a record curl must not take; the TCP reply is whole.
            DnsReferenceServer server = new((_, index) => index == 0
                ? [new DnsReferenceReply { Flags = 0x8380, Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.66")] }]
                : [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync,
                [ReferenceRoute.Udp(53, server), ReferenceRoute.Tcp(53, server)],
                ["-sS", Url]);

            CollectionAssert.AreEqual(new[] { ReferenceTransport.Udp, ReferenceTransport.Tcp }, server.Received.Select(received => received.Transport).ToArray());
            CollectionAssert.AreEqual(server.Received[0].Query.Bytes, server.Received[1].Query.Bytes);
            byte[] query = server.Received[1].Query.Bytes;
            CollectionAssert.AreEqual(new byte[] { 0, (byte)query.Length }.Concat(query).ToArray(), result.Exchanges.Single(exchange => exchange.Transport == ReferenceTransport.Tcp).ClientBytes);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "A", "example.com.\t300\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC4501-2-5")]
    public Task Url_WithNoElementsOrTransport_IsQueriedForTypeAClassInOverUdp() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC4501-2-5"], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.1")] }]);

            CurlRunResult result = await DnsReferenceRun.RunAsync(InProcessCurl.RunAsync, server, ReferenceTransport.Udp, 53, Url);

            DnsReceivedQuery received = server.Received.Single();
            Assert.AreEqual(ReferenceTransport.Udp, received.Transport);
            Assert.AreEqual((ushort)1, received.Query.Type);
            Assert.AreEqual((ushort)1, received.Query.Class);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "A", "example.com.\t60\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-7.2-1")]
    public Task UdpQueryWithNoAnswer_IsSentAgainWithTheSameIdAfterItsWait() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-7.2-1"], async () =>
        {
            // The first two datagrams go unanswered; the third is answered.
            DnsReferenceServer server = new((_, index) => index < 2 ? [] : [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.1")] }]);
            AttemptClock clock = new();

            CurlRunResult result = await RunOnClockAsync(server, clock);

            Assert.HasCount(3, server.Received);
            Assert.IsTrue(server.Received.All(received => received.Transport == ReferenceTransport.Udp));
            Assert.IsTrue(server.Received.All(received => received.Query.Bytes.AsSpan().SequenceEqual(server.Received[0].Query.Bytes)));
            Assert.AreEqual(2 * AttemptWait, clock.Elapsed);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "A", "example.com.\t60\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-7.2-2")]
    public Task UdpQueryNeverAnswered_GivesUpAfterThreeAttemptsWithExit28() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-7.2-2"], async () =>
        {
            DnsReferenceServer server = new((_, _) => []);
            AttemptClock clock = new();

            CurlRunResult result = await RunOnClockAsync(server, clock);

            Assert.AreEqual(28, result.ExitCode);
            Assert.HasCount(3, server.Received);
            Assert.AreEqual(3 * AttemptWait, clock.Elapsed);
            Assert.IsEmpty(result.StandardOutput);
            // curl ends its message line with the platform's newline, CRLF on Windows.
            Assert.AreEqual(
                "curl: (28) The DNS server sent no answer to 3 queries over UDP\n",
                Encoding.UTF8.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
        });

    /// <summary>
    /// Runs <see cref="Url" /> over UDP on <paramref name="clock" />, firing each attempt's wait once the server has
    /// received the query that attempt sent, so no real time passes.
    /// </summary>
    private static async Task<CurlRunResult> RunOnClockAsync(DnsReferenceServer server, AttemptClock clock)
    {
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(53, server)], ["-sS", Url], timeProvider: clock);
        long deadline = Environment.TickCount64 + 30_000;
        int fired = 0;
        while (!run.IsCompleted && Environment.TickCount64 < deadline)
        {
            if (server.Received.Count > fired && clock.FireAttemptWait())
            {
                fired++;
            }
            else
            {
                await Task.Delay(1);
            }
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// A clock that moves only when <see cref="FireAttemptWait" /> fires a pending timer due <see cref="AttemptWait" />
    /// after it was set, so a UDP attempt's wait ends at once and no other timer of the run fires.
    /// </summary>
    private sealed class AttemptClock : TimeProvider
    {
        private static readonly DateTimeOffset Start = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

        private readonly Lock gate = new();

        private readonly List<AttemptTimer> timers = [];

        private long elapsedTicks;

        public TimeSpan Elapsed => TimeSpan.FromTicks(GetTimestamp());

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            lock (gate)
            {
                return elapsedTicks;
            }
        }

        public override DateTimeOffset GetUtcNow() => Start.AddTicks(GetTimestamp());

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            AttemptTimer timer = new(this, callback, state);
            lock (gate)
            {
                timers.Add(timer);
            }

            timer.Change(dueTime, period);
            return timer;
        }

        /// <summary>Moves the clock on by <see cref="AttemptWait" /> and fires the pending attempt wait.</summary>
        /// <returns><see langword="false" /> when no attempt wait is pending yet.</returns>
        public bool FireAttemptWait()
        {
            AttemptTimer? pending;
            lock (gate)
            {
                pending = timers.FirstOrDefault(timer => timer.DueTime == AttemptWait);
                if (pending is null)
                {
                    return false;
                }

                elapsedTicks += AttemptWait.Ticks;
                pending.DueTime = Timeout.InfiniteTimeSpan;
            }

            pending.Fire();
            return true;
        }

        private sealed class AttemptTimer(AttemptClock owner, TimerCallback callback, object? state) : ITimer
        {
            public TimeSpan DueTime { get; set; } = Timeout.InfiniteTimeSpan;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.gate)
                {
                    DueTime = dueTime;
                }

                return true;
            }

            public void Fire() => callback(state);

            public void Dispose()
            {
                lock (owner.gate)
                {
                    owner.timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
