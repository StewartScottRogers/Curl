using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// One TFTP upload over an open channel: sends the write request, then sends each DATA
/// block once the previous one is acknowledged, until the last block is acknowledged or
/// an ERROR packet ends the transfer.
/// </summary>
/// <param name="context">The transfer being performed.</param>
/// <param name="channel">The open channel, which this class does not dispose.</param>
/// <param name="upload">The bytes to send, read from their current position.</param>
/// <remarks>
/// <para>
/// The last block is the first one shorter than the block size, so an upload whose length
/// is an exact multiple of the block size ends with a zero-length DATA block (RFC 1350
/// section 6), and an empty upload is a single zero-length DATA 1, as curl 8.21.0 sends.
/// </para>
/// <para>
/// A datagram shorter than four bytes, an acknowledgement of a block other than the one
/// last sent, an option acknowledgement after the first block has gone, and an opcode an
/// upload never receives are all ignored, and the upload waits for the next datagram.
/// Neither retransmission nor a timeout is implemented yet, so a server that goes silent
/// leaves the receive waiting until the context's token is cancelled.
/// </para>
/// </remarks>
internal sealed class TftpUpload(ITransferContext context, IDatagramChannel channel, Stream upload)
{
    private readonly byte[] receiveBuffer =
        new byte[TftpPackets.MaximumBlockSize + TftpPackets.DataHeaderLength];

    private int blockSize = TftpPackets.DefaultBlockSize;
    private ushort lastSentBlock;
    private bool lastBlockSent;
    private bool firstBlockSent;
    private long bytesTransferred;

    /// <summary>
    /// Runs the upload to its end.
    /// </summary>
    /// <param name="fileName">The file to write, decoded from the URL path.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal async ValueTask<TransferResult> RunAsync(string fileName)
    {
        var transferSize = upload.CanSeek ? upload.Length - upload.Position : 0;
        var request = TftpPackets.BuildWriteRequest(fileName, transferSize, TftpPackets.RequestedBlockSize(context));
        await channel
            .SendAsync(request, channel.ServerEndPoint, context.CancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            var received = await channel
                .ReceiveAsync(receiveBuffer, context.CancellationToken)
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

        switch (TftpPackets.ReadField(receiveBuffer, 0))
        {
            case TftpPackets.AcknowledgementOpcode:
                return await AcceptAcknowledgementAsync(TftpPackets.ReadField(receiveBuffer, 2), received.RemoteEndPoint)
                    .ConfigureAwait(false);
            case TftpPackets.ErrorOpcode:
                return TftpErrorMapping.ToTransferResult(TftpPackets.ReadField(receiveBuffer, 2));
            case TftpPackets.OptionAcknowledgementOpcode when !firstBlockSent:
                blockSize = TftpPackets.ReadAcknowledgedBlockSize(receiveBuffer.AsSpan(2, received.Length - 2));
                return await AcceptAcknowledgementAsync(0, received.RemoteEndPoint).ConfigureAwait(false);
            default:
                return null;
        }
    }

    /// <summary>
    /// Answers the acknowledgement of <paramref name="block" />: ends the transfer when it
    /// acknowledges the last block, otherwise sends the next block to the endpoint the
    /// acknowledgement came from, which is the server's transfer identifier (RFC 1350
    /// section 4).
    /// </summary>
    /// <param name="block">The block acknowledged; 0 acknowledges the write request.</param>
    /// <param name="source">The endpoint the acknowledgement came from.</param>
    /// <returns>
    /// A success when the last block is acknowledged, otherwise <see langword="null" />.
    /// </returns>
    private async ValueTask<TransferResult?> AcceptAcknowledgementAsync(ushort block, EndPoint source)
    {
        if (block != lastSentBlock)
        {
            return null;
        }

        if (lastBlockSent)
        {
            return TransferResult.Success(bytesTransferred);
        }

        await SendNextBlockAsync(source).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Reads up to one block from the upload and sends it as the next DATA packet.
    /// </summary>
    /// <param name="destination">The server's transfer identifier.</param>
    private async ValueTask SendNextBlockAsync(EndPoint destination)
    {
        var payload = new byte[blockSize];
        var filled = 0;
        while (filled < blockSize)
        {
            var read = await upload
                .ReadAsync(payload.AsMemory(filled), context.CancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        lastSentBlock = unchecked((ushort)(lastSentBlock + 1));
        firstBlockSent = true;
        lastBlockSent = filled < blockSize;
        bytesTransferred += filled;

        await channel
            .SendAsync(TftpPackets.BuildData(lastSentBlock, payload.AsSpan(0, filled)), destination, context.CancellationToken)
            .ConfigureAwait(false);
    }
}
