using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// One TFTP download over an open channel: sends the read request, then answers each
/// packet until the last block or an ERROR packet ends the transfer.
/// </summary>
/// <param name="context">The transfer being performed.</param>
/// <param name="channel">The open channel, which this class does not dispose.</param>
/// <remarks>
/// A datagram shorter than four bytes, a DATA block other than the one expected next, and
/// an opcode a download never receives are all ignored, and the download waits for the
/// next datagram. Neither retransmission nor a timeout is implemented yet, so a server
/// that goes silent leaves the receive waiting until the context's token is cancelled.
/// </remarks>
internal sealed class TftpDownload(ITransferContext context, IDatagramChannel channel)
{
    private readonly byte[] buffer =
        new byte[TftpPackets.MaximumBlockSize + TftpPackets.DataHeaderLength];

    private int blockSize = TftpPackets.DefaultBlockSize;
    private ushort expectedBlock = 1;
    private long bytesTransferred;

    /// <summary>
    /// Runs the download to its end.
    /// </summary>
    /// <param name="fileName">The file to read, decoded from the URL path.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal async ValueTask<TransferResult> RunAsync(string fileName)
    {
        await channel
            .SendAsync(TftpPackets.BuildReadRequest(fileName), channel.ServerEndPoint, context.CancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            var received = await channel
                .ReceiveAsync(buffer, context.CancellationToken)
                .ConfigureAwait(false);

            if (await AnswerAsync(received).ConfigureAwait(false) is { } outcome)
            {
                return outcome;
            }
        }
    }

    /// <summary>
    /// Answers one received datagram.
    /// </summary>
    /// <param name="received">The datagram's length and source.</param>
    /// <returns>The transfer's outcome when this datagram ended it, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> AnswerAsync(DatagramReceived received)
    {
        if (received.Length < TftpPackets.DataHeaderLength)
        {
            return null;
        }

        switch (TftpPackets.ReadField(buffer, 0))
        {
            case TftpPackets.DataOpcode:
                return await AcceptDataAsync(received).ConfigureAwait(false);
            case TftpPackets.ErrorOpcode:
                return TftpErrorMapping.ToTransferResult(TftpPackets.ReadField(buffer, 2));
            case TftpPackets.OptionAcknowledgementOpcode:
                blockSize = TftpPackets.ReadAcknowledgedBlockSize(buffer.AsSpan(2, received.Length - 2));
                await AcknowledgeAsync(0, received.RemoteEndPoint).ConfigureAwait(false);
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Writes the expected DATA block to the output and acknowledges it to the endpoint it
    /// came from, which is the server's transfer identifier (RFC 1350 section 4).
    /// </summary>
    /// <param name="received">The DATA datagram's length and source.</param>
    /// <returns>
    /// A success when the block is shorter than the block size and so the last, otherwise
    /// <see langword="null" />.
    /// </returns>
    private async ValueTask<TransferResult?> AcceptDataAsync(DatagramReceived received)
    {
        var block = TftpPackets.ReadField(buffer, 2);
        if (block != expectedBlock)
        {
            return null;
        }

        var payloadLength = received.Length - TftpPackets.DataHeaderLength;
        await context.Output
            .WriteAsync(buffer.AsMemory(TftpPackets.DataHeaderLength, payloadLength), context.CancellationToken)
            .ConfigureAwait(false);
        bytesTransferred += payloadLength;

        await AcknowledgeAsync(block, received.RemoteEndPoint).ConfigureAwait(false);
        expectedBlock = unchecked((ushort)(block + 1));

        return payloadLength < blockSize ? TransferResult.Success(bytesTransferred) : null;
    }

    private ValueTask AcknowledgeAsync(ushort block, EndPoint destination) =>
        channel.SendAsync(TftpPackets.BuildAcknowledgement(block), destination, context.CancellationToken);
}
