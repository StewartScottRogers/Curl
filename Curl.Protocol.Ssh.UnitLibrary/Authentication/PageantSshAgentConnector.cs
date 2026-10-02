using System.Buffers.Binary;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The <see cref="ISshAgentConnector" /> for PuTTY's Pageant, which libssh2 1.11.1 tries
/// before the OpenSSH pipe on Windows (ADR-0304): no agent when Pageant's window is not
/// found, otherwise a connection whose every whole request frame is one
/// <c>agent_transact_pageant</c>.
/// </summary>
/// <param name="window">Finds Pageant's window and exchanges one request with it.</param>
internal sealed class PageantSshAgentConnector(IPageantWindow window) : ISshAgentConnector
{
    /// <summary>libssh2's <c>PAGEANT_MAX_MSGLEN</c>: the size of the file mapping, request frame included.</summary>
    internal const int MaximumMessageLength = 8192;

    private const int LengthSize = 4;

    /// <inheritdoc />
    public ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Stream?>(window.IsRunning() ? new PageantAgentStream(Transact) : null);

    /// <summary>
    /// Exchanges one request frame with Pageant, as libssh2's <c>agent_transact_pageant</c>
    /// does: a frame longer than the mapping, a window gone since the connection, or a
    /// message answered with zero gives no answer.
    /// </summary>
    /// <param name="frame">The request's length and body.</param>
    /// <returns>The answer's length and body, or no bytes when the exchange fails.</returns>
    internal byte[] Transact(byte[] frame)
    {
        if (frame.Length > MaximumMessageLength || !window.IsRunning())
        {
            return [];
        }

        byte[] mapping = new byte[MaximumMessageLength];
        frame.CopyTo(mapping, 0);
        if (!window.Exchange(mapping))
        {
            return [];
        }

        // libssh2 accepts a length up to 8192 and copies past the mapping's end; an answer
        // that does not fit in the mapping is refused here instead (ADR-0304).
        uint length = BinaryPrimitives.ReadUInt32BigEndian(mapping);
        return length > MaximumMessageLength - LengthSize ? [] : mapping[..(LengthSize + (int)length)];
    }
}
