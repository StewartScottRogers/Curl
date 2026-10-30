using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// The inside of the tunnel the IP stack sends and receives over (whitepaper section 5.4.6): each IP packet sent is
/// sealed into a transport data message, and each datagram received is opened, keeping only the packets and dropping
/// keepalives, other messages and anything that fails to open. It keeps the session alive as sections 6.2 and 6.5
/// say: once the sending session <see cref="WireGuardTransportSession.NeedsRekey" />, a send also sends a new
/// initiation (again after each Rekey-Timeout without a response); the response moves sending to the new session,
/// and the old one still opens what the peer sealed under it. A keepalive goes out Keepalive-Timeout after a packet
/// arrived with nothing sent since, and after every <c>PersistentKeepalive</c> seconds without traffic.
/// </summary>
internal sealed class WireGuardPacketTunnel : IIpPacketChannel, IDisposable
{
    /// <summary>Section 6.1's Keepalive-Timeout.</summary>
    public static readonly TimeSpan KeepaliveTimeout = TimeSpan.FromSeconds(10);

    private readonly Lock stateLock = new();

    private readonly IWireGuardDatagramChannel channel;

    private readonly WireGuardHandshakeInitiator initiator;

    private readonly TimeProvider timeProvider;

    private readonly TimeSpan persistentKeepaliveInterval;

    private readonly ITimer passiveKeepalive;

    private readonly ITimer persistentKeepalive;

    private WireGuardTransportSession current;

    private WireGuardTransportSession? previous;

    private long? rekeyInitiatedAt;

    private bool passiveKeepaliveArmed;

    /// <summary>Starts the tunnel on the session the first handshake gave.</summary>
    /// <param name="channel">The datagram path to the peer.</param>
    /// <param name="session">The transport keys the handshake gave.</param>
    /// <param name="initiator">The initiator that gave them, which writes each rekey's initiation.</param>
    /// <param name="timeProvider">The clock the rekey and keepalive timers run on.</param>
    /// <param name="persistentKeepaliveInterval">The peer's <c>PersistentKeepalive</c>, or <see cref="Timeout.InfiniteTimeSpan" /> for none.</param>
    public WireGuardPacketTunnel(
        IWireGuardDatagramChannel channel,
        WireGuardTransportSession session,
        WireGuardHandshakeInitiator initiator,
        TimeProvider timeProvider,
        TimeSpan persistentKeepaliveInterval)
    {
        this.channel = channel;
        current = session;
        this.initiator = initiator;
        this.timeProvider = timeProvider;
        this.persistentKeepaliveInterval = persistentKeepaliveInterval;
        passiveKeepalive = timeProvider.CreateTimer(_ => SendKeepalive(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        persistentKeepalive = timeProvider.CreateTimer(_ => SendKeepalive(), null, persistentKeepaliveInterval, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Completes the handshake with <paramref name="peer" /> over <paramref name="channel" /> and starts a tunnel on its session.</summary>
    /// <param name="wireGuardInterface">The configuration's interface.</param>
    /// <param name="peer">The peer cryptokey routing picked.</param>
    /// <param name="channel">The datagram path to the peer.</param>
    /// <param name="timeProvider">The clock the handshake and the tunnel's timers run on.</param>
    /// <param name="random">The randomness for the handshakes.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The tunnel.</returns>
    /// <exception cref="TimeoutException">No response arrived within Rekey-Attempt-Time.</exception>
    public static async Task<WireGuardPacketTunnel> OpenAsync(
        WireGuardInterface wireGuardInterface,
        WireGuardPeer peer,
        IWireGuardDatagramChannel channel,
        TimeProvider timeProvider,
        RandomNumberGenerator random,
        CancellationToken cancellationToken)
    {
        WireGuardHandshakeInitiator initiator = WireGuardHandshakeInitiator.ForPeer(wireGuardInterface, peer, timeProvider, random);
        WireGuardTransportSession session = await WireGuardTunnelHandshake.ConnectAsync(initiator, channel, timeProvider, cancellationToken).ConfigureAwait(false);
        return new WireGuardPacketTunnel(channel, session, initiator, timeProvider, PersistentKeepaliveIntervalOf(peer));
    }

    /// <summary>The peer's <c>PersistentKeepalive</c> as an interval, or <see cref="Timeout.InfiniteTimeSpan" /> when it is off.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The interval.</returns>
    public static TimeSpan PersistentKeepaliveIntervalOf(WireGuardPeer peer) =>
        peer.PersistentKeepalive is int seconds ? TimeSpan.FromSeconds(seconds) : Timeout.InfiniteTimeSpan;

    /// <inheritdoc />
    /// <remarks>An empty packet is a keepalive.</remarks>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        byte[] message;
        byte[]? initiation;
        lock (stateLock)
        {
            message = current.Seal(packet.Span);
            initiation = RekeyInitiationIfDue();
            passiveKeepaliveArmed = false;
            passiveKeepalive.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            persistentKeepalive.Change(persistentKeepaliveInterval, Timeout.InfiniteTimeSpan);
        }

        await channel.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (initiation is not null)
        {
            await channel.SendAsync(initiation, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadOnlyMemory<byte> datagram = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (TakeDatagram(datagram.Span) is { Length: > 0 } packet)
            {
                return packet;
            }
        }
    }

    /// <summary>Stops the keepalives and zeroes the keys of every session.</summary>
    public void Dispose()
    {
        passiveKeepalive.Dispose();
        persistentKeepalive.Dispose();
        lock (stateLock)
        {
            current.Dispose();
            previous?.Dispose();
        }
    }

    private static byte[]? OpenWith(WireGuardTransportSession? session, ReadOnlySpan<byte> datagram) =>
        session is not null && session.TryOpen(datagram, out byte[]? packet) ? packet : null;

    /// <summary>A new initiation when the sending session needs a rekey and none was sent within Rekey-Timeout (section 6.2).</summary>
    private byte[]? RekeyInitiationIfDue()
    {
        if (!current.NeedsRekey || (rekeyInitiatedAt is long initiatedAt && timeProvider.GetElapsedTime(initiatedAt) < WireGuardTunnelHandshake.RekeyTimeout))
        {
            return null;
        }

        rekeyInitiatedAt = timeProvider.GetTimestamp();
        return initiator.CreateInitiation();
    }

    /// <summary>
    /// Opens a transport data message under the current or the previous session, or takes a rekey's response
    /// (moving sending to its session) or cookie reply.
    /// </summary>
    /// <returns>The padded packet, empty for a keepalive, or <see langword="null" /> for anything else.</returns>
    private byte[]? TakeDatagram(ReadOnlySpan<byte> datagram)
    {
        lock (stateLock)
        {
            byte[]? packet = OpenWith(current, datagram) ?? OpenWith(previous, datagram);
            if (packet is not null)
            {
                NoteReceived(packet);
            }
            else if (initiator.TryConsumeResponse(datagram, out WireGuardTransportSession? next))
            {
                previous?.Dispose();
                previous = current;
                current = next;
                rekeyInitiatedAt = null;
            }
            else
            {
                _ = initiator.TryConsumeCookieReply(datagram);
            }

            return packet;
        }
    }

    /// <summary>Arms the passive keepalive for a packet that is not itself a keepalive, and restarts the persistent one (section 6.5).</summary>
    private void NoteReceived(byte[] packet)
    {
        if (packet.Length > 0 && !passiveKeepaliveArmed)
        {
            passiveKeepaliveArmed = true;
            passiveKeepalive.Change(KeepaliveTimeout, Timeout.InfiniteTimeSpan);
        }

        persistentKeepalive.Change(persistentKeepaliveInterval, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Sends a keepalive from a timer; a failure (keys expired, socket closed) is observed and dropped, as nothing waits on it.</summary>
    private void SendKeepalive() =>
        _ = SendAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None).AsTask().ContinueWith(
            static task => task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
