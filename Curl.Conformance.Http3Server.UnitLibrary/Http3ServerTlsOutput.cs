using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>Collects what one <see cref="Http3ServerTlsHandshake.Receive" /> call sends and installs.</summary>
internal sealed class Http3ServerTlsOutput
{
    private readonly List<TlsHandshakeBytes> bytesToSend = [];

    private readonly List<Tls13TrafficSecret> secretsInstalled = [];

    /// <summary>Adds handshake bytes to send at <paramref name="level" />.</summary>
    /// <param name="level">The encryption level to send them at.</param>
    /// <param name="bytes">The handshake messages.</param>
    public void Send(TlsEncryptionLevel level, byte[] bytes) => bytesToSend.Add(new TlsHandshakeBytes(level, bytes));

    /// <summary>Adds a traffic secret installed for one level and direction.</summary>
    /// <param name="level">The encryption level.</param>
    /// <param name="direction">Read for the client's traffic, Write for the server's.</param>
    /// <param name="secret">The traffic secret.</param>
    public void Install(TlsEncryptionLevel level, TlsTrafficDirection direction, byte[] secret) =>
        secretsInstalled.Add(new Tls13TrafficSecret(level, direction, secret));

    /// <summary>Returns the collected output.</summary>
    /// <param name="isComplete">Whether the handshake has completed.</param>
    /// <returns>The output, with no failure.</returns>
    public Tls13HandshakeOutput Build(bool isComplete) => new(bytesToSend, secretsInstalled, isComplete, null);
}
