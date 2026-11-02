using System.Net;
using Curl.Api.Options.Network;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the factories the dialing runner calls with its run clock time on that clock (BL-4508): the
/// <c>CURL_DNS_SERVER</c> resolver asking a silent server gives up after its three attempts (2, 4 and 8
/// seconds) once the test advances a fake clock past each, with no real wait; on the real clock the same
/// lookup would take 14 seconds and the test's wait for the fake clock's timers would give up first.
/// </summary>
[TestClass]
public sealed class CurlCompositionDialingClockTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DnsServerVariableResolverOf_WithAClock_TimesOutASilentServerOnThatClock()
    {
        Cgr109Tests.TimerFiringClock clock = new();
        SilentDatagramConnector server = new();
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(Curl.Cli.CommandLineParser.Parse(["-4", "http://dns.example/"], _ => true).Options!);
        DnsServerResolver resolver = (DnsServerResolver)EngineComposition.DnsServerVariableResolverOf(network, "127.0.0.1:5353", new LoopbackDnsResolver(), server, clock);

        Task<DnsResolution> resolution = resolver.ResolveWithFailureReasonAsync("dns.example", TestContext.CancellationToken).AsTask();
        await clock.WhenTimersDueAsync(2_000, 1);
        clock.Advance(2_000);
        await clock.WhenTimersDueAsync(6_000, 1);
        clock.Advance(4_000);
        await clock.WhenTimersDueAsync(14_000, 1);
        clock.Advance(8_000);
        DnsResolution resolved = await resolution.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsEmpty(resolved.Addresses);
        Assert.AreEqual(3, server.Opened);
    }

    [TestMethod]
    public void DnsServerVariableResolverOf_WithoutAClock_StillMakesTheServerResolver()
    {
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(Curl.Cli.CommandLineParser.Parse(["http://dns.example/"], _ => true).Options!);

        IDnsResolver chosen = EngineComposition.DnsServerVariableResolverOf(network, "127.0.0.1:5353", new LoopbackDnsResolver(), new SilentDatagramConnector());

        Assert.IsInstanceOfType<DnsServerResolver>(chosen);
    }

    [TestMethod]
    public void CreateDatagramConnectorQuicDialer_WithAndWithoutAClock_MakesADialer()
    {
        QuicDialer withClock = EngineComposition.CreateDatagramConnectorQuicDialer(EngineTraceComponents.None, new TlsClientOptions(), new SilentDatagramConnector(), new Cgr109Tests.TimerFiringClock());
        QuicDialer withoutClock = EngineComposition.CreateDatagramConnectorQuicDialer(EngineTraceComponents.None, new TlsClientOptions(), new SilentDatagramConnector());

        Assert.AreNotSame(withClock, withoutClock);
    }

    /// <summary>A datagram connector whose every channel takes each query and never answers.</summary>
    private sealed class SilentDatagramConnector : IDatagramConnector
    {
        private int opened;

        /// <summary>Gets how many channels were opened.</summary>
        public int Opened => Volatile.Read(ref opened);

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref opened);
            return ValueTask.FromResult(DatagramOpenResult.Opened(new Channel(new IPEndPoint(IPAddress.Parse(host), port))));
        }

        private sealed class Channel(IPEndPoint endPoint) : IDatagramChannel
        {
            public EndPoint ServerEndPoint => endPoint;

            public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) => ValueTask.CompletedTask;

            public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new OperationCanceledException(cancellationToken);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>A resolver answering every name with the loopback address.</summary>
    private sealed class LoopbackDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }
}
