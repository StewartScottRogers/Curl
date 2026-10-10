using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The server's side of one SSH connection's transport layer (RFC 4253): the identification
/// exchange, then packets framed by the SSH client's own <see cref="SshPacketReader" /> and
/// <see cref="SshPacketWriter" />, unencrypted until key exchange changes their protection.
/// </summary>
internal sealed class SshServerTransport
{
    /// <summary>
    /// The identification string the server sends: OpenSSH's, as the SSH client's tests
    /// already answer with.
    /// </summary>
    internal const string ServerIdentification = "SSH-2.0-OpenSSH_9.7";

    private readonly IConnection connection;

    private readonly SshConnectionReader connectionReader;

    private readonly SshPacketReader packetReader;

    private readonly SshPacketWriter packetWriter;

    /// <summary>
    /// Wraps the server's end of a connection.
    /// </summary>
    /// <param name="connection">The server's end.</param>
    /// <param name="randomSource">Where packet padding comes from.</param>
    internal SshServerTransport(IConnection connection, ISshRandomSource randomSource)
    {
        this.connection = connection;
        connectionReader = new SshConnectionReader(connection);
        packetReader = new SshPacketReader(connectionReader);
        packetWriter = new SshPacketWriter(connection, randomSource);
    }

    /// <summary>
    /// Gets the client's identification string, once <see cref="ExchangeIdentificationAsync" /> has read it.
    /// </summary>
    internal string? ClientIdentification { get; private set; }

    /// <summary>
    /// Sends <see cref="ServerIdentification" /> and reads the client's identification line,
    /// skipping any line before it that does not start with <c>SSH-</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The client's identification string.</returns>
    /// <exception cref="IOException">The client closed the connection before identifying itself.</exception>
    internal async ValueTask<string> ExchangeIdentificationAsync(CancellationToken cancellationToken)
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes(ServerIdentification + "\r\n"), cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        string line;
        do
        {
            line = await connectionReader.ReadLineAsync(SshIdentificationExchange.MaximumLineLength, cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The SSH client closed the connection before sending its identification.");
        }
        while (!line.StartsWith("SSH-", StringComparison.Ordinal));

        ClientIdentification = line;
        return line;
    }

    /// <summary>
    /// Reads the next packet's payload, message number first.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The payload.</returns>
    internal ValueTask<byte[]> ReadPacketAsync(CancellationToken cancellationToken) => packetReader.ReadAsync(cancellationToken);

    /// <summary>
    /// Frames and sends one payload, message number first.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the packet is written.</returns>
    internal ValueTask WritePacketAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => packetWriter.WriteAsync(payload, cancellationToken);
}
