using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// The protocol version exchange of RFC 4253 section 4.2: sends curl's identification
/// string and reads the server's, skipping the lines a server may send before it.
/// </summary>
internal static class SshIdentificationExchange
{
    /// <summary>
    /// The identification string both reference builds of curl 8.21.0 send, without its
    /// CR LF (ADR-0122).
    /// </summary>
    internal const string ClientIdentification = "SSH-2.0-libssh2_1.11.1";

    /// <summary>
    /// The longest line read while looking for the server's identification, LF included.
    /// RFC 4253 allows 255 bytes; libssh2 1.11.1 accepts longer (a 310-byte identification
    /// was measured to pass), so the limit only bounds memory.
    /// </summary>
    internal const int MaximumLineLength = 8192;

    /// <summary>
    /// Sends <see cref="ClientIdentification" /> and reads lines until one starts
    /// <c>SSH-</c>. As libssh2 does, any version after <c>SSH-</c> is accepted here.
    /// </summary>
    /// <param name="connection">The connection to write to.</param>
    /// <param name="reader">The buffered reader over the same connection.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The server's identification string, without its line ending.</returns>
    /// <exception cref="SshTransferException">
    /// The peer closed, or sent an over-long line, before a line starting <c>SSH-</c>:
    /// exit 2, <c>Failure establishing ssh session: -13, Failed getting banner</c>. The
    /// connection failed - the peer reset or aborted it - before that line: the same with
    /// <c>-43</c> in place of <c>-13</c> (ADR-0283).
    /// </exception>
    internal static async ValueTask<string> ExchangeAsync(
        IConnection connection,
        SshConnectionReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(Encoding.ASCII.GetBytes(ClientIdentification + "\r\n"), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return await ReadServerIdentificationAsync(reader, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.SocketReceive, Libssh2ErrorCode.FailedGettingBanner);
        }
    }

    private static async ValueTask<string> ReadServerIdentificationAsync(SshConnectionReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line = await reader.ReadLineAsync(MaximumLineLength, cancellationToken).ConfigureAwait(false)
                ?? throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.SocketDisconnect, Libssh2ErrorCode.FailedGettingBanner);
            if (line.StartsWith("SSH-", StringComparison.Ordinal))
            {
                return line;
            }
        }
    }
}
