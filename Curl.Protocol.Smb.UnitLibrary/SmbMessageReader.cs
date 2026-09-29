using System.Buffers.Binary;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Reads whole SMB messages from the connection as curl 8.21.0's <c>smb_recv_message</c>
/// does: into one buffer of <c>MAX_MESSAGE_SIZE</c> bytes, until the NetBIOS length says
/// the message is complete, with curl's checks on the frame and on the word and byte
/// counts.
/// </summary>
/// <param name="connection">The connection to read from.</param>
/// <param name="timeProvider">The clock a closed connection waits on.</param>
/// <param name="transferLog">
/// Where each received message's command and status is logged; <see langword="null" />, the
/// default, for nowhere.
/// </param>
internal sealed class SmbMessageReader(IConnection connection, TimeProvider timeProvider, SmbTransferLog? transferLog = null)
{
    /// <summary>curl's <c>MAX_MESSAGE_SIZE</c>, the most bytes one message may take.</summary>
    public const int MaxMessageSize = 0x9000;

    private const int WordCountOffset = SmbMessageHeader.Length;

    private readonly byte[] buffer = new byte[MaxMessageSize];

    /// <summary>
    /// Reads the next message, discarding whatever was left over from the one before, as
    /// <c>smb_pop_message</c> does.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>
    /// The message, holding every byte received for it (curl's <c>got</c>), which may run
    /// past its NetBIOS length; or exit 56 (<see cref="CurlExitCode.RecvError" />) with
    /// curl's message for a frame that is too large or too small, or whose byte count runs
    /// past it.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. A server that closes the
    /// connection before a message is whole is waited on until it is, because curl 8.21.0
    /// keeps polling a closed connection until <c>-m</c> ends the transfer (measured).
    /// </exception>
    public async ValueTask<SmbReceivedMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        int got = 0;
        while (true)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(got), cancellationToken).ConfigureAwait(false);
            await WaitUntilCancelledIfClosedAsync(read, cancellationToken).ConfigureAwait(false);
            got += read;
            if (got < SmbMessageHeader.NetBiosHeaderLength)
            {
                continue;
            }

            int frameSize = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(2)) + SmbMessageHeader.NetBiosHeaderLength;
            if (FrameSizeFailure(frameSize) is { } failure)
            {
                return failure;
            }

            if (got >= frameSize)
            {
                return CheckCounts(frameSize, got);
            }
        }
    }

    // A read of zero is the server closing; curl keeps polling until -m, so this waits
    // until the transfer is cancelled and never completes normally.
    private Task WaitUntilCancelledIfClosedAsync(int read, CancellationToken cancellationToken) =>
        read == 0 ? Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken) : Task.CompletedTask;

    private static SmbReceivedMessage? FrameSizeFailure(int frameSize) =>
        frameSize > MaxMessageSize ? SmbReceivedMessage.Failed($"too large NetBIOS frame size {frameSize}")
        : frameSize < SmbMessageHeader.Length ? SmbReceivedMessage.Failed($"too small NetBIOS frame size {frameSize}")
        : null;

    private SmbReceivedMessage CheckCounts(int frameSize, int got)
    {
        int messageSize = SmbMessageHeader.Length;
        if (frameSize >= messageSize + 1)
        {
            messageSize += 1 + (buffer[WordCountOffset] * 2);
            if (frameSize >= messageSize + 2)
            {
                messageSize += 2 + BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(messageSize));
                if (frameSize < messageSize)
                {
                    return SmbReceivedMessage.Failed(SmbMessages.ReceiveFailed);
                }
            }
        }

        byte[] message = buffer.AsSpan(0, got).ToArray();
        (transferLog ?? SmbTransferLog.None).Received(message);
        return SmbReceivedMessage.Received(message);
    }
}
