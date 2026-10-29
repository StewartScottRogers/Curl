using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>Datagrams through <see cref="IDatagramCarryingConnection" /> on a <see cref="QuicConnection" /> over the in-memory server.</summary>
[TestClass]
public sealed class QuicConnectionDatagramTests
{
    private const ulong Advertised = 65535;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task SendAndReceiveDatagramAsync_BothAdvertise_CarriesDatagramsBothWays()
    {
        Diagnostics.Arrange("datagrams", "'ping' to the server, 'pong' back");
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            IDatagramCarryingConnection datagrams = connection;
            Diagnostics.Act("max datagram payload length", datagrams.MaxDatagramPayloadLength);
            Diagnostics.Assert("max datagram payload length", QuicClientConnectionState.MaximumDatagramPayloadLength, datagrams.MaxDatagramPayloadLength);
            Assert.AreEqual(QuicClientConnectionState.MaximumDatagramPayloadLength, datagrams.MaxDatagramPayloadLength);

            await datagrams.SendDatagramAsync("ping"u8.ToArray(), TestContext.CancellationToken);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicDatagramFrame>().Count > 0);
            Task<ReadOnlyMemory<byte>> received = datagrams.ReceiveDatagramAsync(TestContext.CancellationToken).AsTask();
            channel.FromServer(new QuicDatagramFrame("pong"u8.ToArray()));
            ReadOnlyMemory<byte> pong = await received;

            string sent = Encoding.ASCII.GetString(channel.Sent<QuicDatagramFrame>().Single().Data.Span);
            string answer = Encoding.ASCII.GetString(pong.Span);
            Diagnostics.Act("datagrams (sent, received)", (sent, answer));
            Diagnostics.Assert("datagrams (sent, received)", ("ping", "pong"), (sent, answer));
            Assert.AreEqual(("ping", "pong"), (sent, answer));
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task SendDatagramAsync_Cancelled_QueuesNothing()
    {
        Diagnostics.Arrange("cancellation", "already requested");
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await connection.SendDatagramAsync("x"u8.ToArray(), new CancellationToken(canceled: true)));

            int sent = channel.Sent<QuicDatagramFrame>().Count;
            Diagnostics.Act("DATAGRAM frames sent", sent);
            Diagnostics.Assert("DATAGRAM frames sent", 0, sent);
            Assert.AreEqual(0, sent);
        }
    }

    private async Task<(QuicConnection Connection, QuicTestLiveChannel Channel)> ConnectAsync()
    {
        ManualTimerTimeProvider clock = new();
        QuicTestServer server = new() { ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { MaxDatagramFrameSize = Advertised } };
        QuicTestLiveChannel channel = new(server);
        QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { TransportParameters = SmallClientLimits with { MaxDatagramFrameSize = Advertised } }, clock: clock);
        object? handshakeFailure;
        using (Diagnostics.Phase("handshake"))
        {
            handshakeFailure = await new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, TestContext.CancellationToken);
        }

        Diagnostics.Act("handshake failure", handshakeFailure?.ToString() ?? "none");
        Assert.IsNull(handshakeFailure);
        return (new QuicConnection(handshake, channel, clock), channel);
    }
}
