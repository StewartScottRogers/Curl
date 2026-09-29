namespace Curl.Tls;

/// <summary>Collects what one handshake step sends and installs, joining consecutive sends at the same level.</summary>
internal sealed class Tls13HandshakeOutputBuilder
{
    private readonly List<TlsHandshakeBytes> bytesToSend = [];
    private readonly List<Tls13TrafficSecret> secretsInstalled = [];

    /// <summary>Queues <paramref name="message" /> to send at <paramref name="level" />.</summary>
    /// <param name="level">The encryption level.</param>
    /// <param name="message">A whole handshake message, header included.</param>
    public void Send(TlsEncryptionLevel level, byte[] message)
    {
        if (bytesToSend.Count > 0 && bytesToSend[^1].Level == level)
        {
            bytesToSend[^1] = new TlsHandshakeBytes(level, [.. bytesToSend[^1].Bytes, .. message]);
            return;
        }

        bytesToSend.Add(new TlsHandshakeBytes(level, message));
    }

    /// <summary>Records that <paramref name="secret" /> now protects <paramref name="level" /> in <paramref name="direction" />.</summary>
    /// <param name="level">The encryption level.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="secret">The traffic secret.</param>
    public void Install(TlsEncryptionLevel level, TlsTrafficDirection direction, byte[] secret) =>
        secretsInstalled.Add(new Tls13TrafficSecret(level, direction, secret));

    /// <summary>Returns the step's output.</summary>
    /// <param name="isComplete">Whether the handshake has completed.</param>
    /// <param name="failure">Why it failed, if it has.</param>
    /// <returns>The output.</returns>
    public Tls13HandshakeOutput Build(bool isComplete, TlsHandshakeFailure? failure) =>
        new([.. bytesToSend], [.. secretsInstalled], isComplete, failure);
}
