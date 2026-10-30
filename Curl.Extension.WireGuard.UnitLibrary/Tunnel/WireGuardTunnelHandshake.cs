using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// Runs the handshake as whitepaper section 6.4 times it: a new initiation every Rekey-Timeout until a response
/// arrives, giving up once Rekey-Attempt-Time has passed.
/// </summary>
internal static class WireGuardTunnelHandshake
{
    /// <summary>Rekey-Attempt-Time over Rekey-Timeout: 90 seconds of initiations (section 6.1).</summary>
    public const int MaximumAttempts = 18;

    /// <summary>Rekey-Timeout: how long one initiation waits for its response (section 6.1).</summary>
    public static readonly TimeSpan RekeyTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Completes a handshake over <paramref name="channel" />.</summary>
    /// <param name="initiator">The initiator for the peer.</param>
    /// <param name="channel">The datagram path to the peer.</param>
    /// <param name="timeProvider">The clock Rekey-Timeout runs on.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The transport session.</returns>
    /// <exception cref="TimeoutException">No response arrived within Rekey-Attempt-Time.</exception>
    public static async Task<WireGuardTransportSession> ConnectAsync(
        WireGuardHandshakeInitiator initiator,
        IWireGuardDatagramChannel channel,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            using CancellationTokenSource timeout = new(RekeyTimeout, timeProvider);
            using CancellationTokenSource attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                return await initiator.ConnectAsync(channel, attemptCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        throw new TimeoutException($"the WireGuard handshake had no response after {MaximumAttempts} initiations");
    }
}
