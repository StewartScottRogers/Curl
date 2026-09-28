using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Reads the reply to an upgrade request from the connection up to the blank line that ends
/// its head, keeping any bytes that arrived after it for the frame reader (ADR-0128).
/// </summary>
/// <remarks>
/// Lines may end in CRLF or a bare LF. Measured (BL-580): a connection closed before the head
/// ends, with or without bytes received, fails with 52, <c>Empty reply from server</c>; bytes
/// that cannot begin <c>HTTP/</c> fail with 1 as soon as they arrive (<see cref="WsStatusLine" />).
/// A head longer than <see cref="MaximumHeadLength" /> fails with 100, as the HTTP library's
/// longest line does; no read goes past that length, so the limit is exact. A failed read
/// fails with 56 (<see cref="WsIoFailures" />).
/// </remarks>
internal static class WsUpgradeResponseReader
{
    /// <summary>The most bytes a reply head may take before the transfer fails with 100.</summary>
    internal const int MaximumHeadLength = 102400;

    private const string EmptyReply = "Empty reply from server";

    private const string HeadTooLarge = "A value or data field grew larger than allowed";

    private const int InitialBufferSize = 16384;

    /// <summary>Reads the reply head.</summary>
    /// <param name="connection">The connection the upgrade request was sent on.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The status code, the head and the bytes that followed it.</returns>
    /// <exception cref="WsTransferException">The reply ended early or curl refuses it.</exception>
    internal static async ValueTask<WsUpgradeResponse> ReadAsync(IConnection connection, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[InitialBufferSize];
        int received = 0;
        int lineStart = 0;
        while (true)
        {
            int headLength = FindHeadEnd(buffer.AsSpan(0, received), ref lineStart);
            if (headLength > 0)
            {
                return Parse(buffer[..headLength], buffer[headLength..received]);
            }

            buffer = MakeRoom(buffer, received);
            int room = Math.Min(buffer.Length, MaximumHeadLength) - received;
            int read = await ReceiveAsync(connection, buffer.AsMemory(received, room), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new WsTransferException(CurlExitCode.GotNothing, EmptyReply);
            }

            received += read;

            if (!WsStatusLine.CanBegin(buffer.AsSpan(0, received)))
            {
                throw new WsTransferException(CurlExitCode.UnsupportedProtocol, WsStatusLine.Http09NotAllowed);
            }
        }
    }

    /// <summary>
    /// Finds the end of the head: the first empty line after the status line. Complete lines
    /// already scanned are skipped by advancing <paramref name="lineStart" />.
    /// </summary>
    /// <param name="received">The bytes received so far.</param>
    /// <param name="lineStart">Where the first line not yet scanned starts.</param>
    /// <returns>The length of the head, blank line included, or 0 while it has not ended.</returns>
    private static int FindHeadEnd(ReadOnlySpan<byte> received, ref int lineStart)
    {
        while (true)
        {
            int lineFeed = received[lineStart..].IndexOf((byte)'\n');
            if (lineFeed < 0)
            {
                return 0;
            }

            bool isBlank = lineStart > 0 && (lineFeed == 0 || (lineFeed == 1 && received[lineStart] == '\r'));
            lineStart += lineFeed + 1;
            if (isBlank)
            {
                return lineStart;
            }
        }
    }

    private static async ValueTask<int> ReceiveAsync(IConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw WsIoFailures.ReceiveFailed(exception);
        }
    }

    private static byte[] MakeRoom(byte[] buffer, int received)
    {
        if (received >= MaximumHeadLength)
        {
            throw new WsTransferException(CurlExitCode.TooLarge, HeadTooLarge);
        }

        if (received < buffer.Length)
        {
            return buffer;
        }

        Array.Resize(ref buffer, buffer.Length * 2);
        return buffer;
    }

    private static WsUpgradeResponse Parse(byte[] head, byte[] remaining)
    {
        int lineFeed = Array.IndexOf(head, (byte)'\n');
        string statusLine = Encoding.Latin1.GetString(head, 0, lineFeed).TrimEnd('\r');
        return new WsUpgradeResponse(WsStatusLine.ParseStatusCode(statusLine), head, remaining);
    }
}
