using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// One TFTP upload over an open channel: sends the write request, then sends each DATA
/// block once the previous one is acknowledged, until the last block is acknowledged or
/// an ERROR packet ends the transfer, re-sending to a silent server on curl 8.21.0's
/// schedule until the retries or the time run out.
/// </summary>
/// <param name="context">The transfer being performed.</param>
/// <param name="channel">The open channel, which this class does not dispose.</param>
/// <param name="upload">The bytes to send, read from their current position.</param>
/// <param name="startTimestamp">
/// The <see cref="ITransferContext.TimeProvider" /> timestamp the transfer started at,
/// which <see cref="ITransferContext.MaxTime" /> and the connect timeout count from.
/// </param>
/// <remarks>
/// <para>
/// The last block is the first one shorter than the block size, so an upload whose length
/// is an exact multiple of the block size ends with a zero-length DATA block (RFC 1350
/// section 6), and an empty upload is a single zero-length DATA 1, as curl 8.21.0 sends.
/// </para>
/// <para>
/// The write request's <c>timeout</c> and the schedule it is re-sent on come from the
/// time left, as for a download (<see cref="TftpTimeLimits" />); an unanswered write
/// request ends with exit 7 (<see cref="CurlExitCode.CouldntConnect" />). The first ACK or
/// OACK re-derives the schedule, and the last DATA block is re-sent until the retries run
/// out, ending with exit 28 (<see cref="CurlExitCode.OperationTimedOut" />)
/// <c>Timeout was reached</c>; <see cref="ITransferContext.MaxTime" /> passing ends it with
/// exit 28 and <c>0 bytes received</c>, since an upload receives none.
/// </para>
/// <para>
/// As curl does, an acknowledgement of a block other than the one last sent re-sends it at
/// once and counts a retry, and one retry too many ends the upload with exit 55
/// (<see cref="CurlExitCode.SendError" />). A datagram shorter than four bytes also
/// re-sends at once and counts a retry, and from then on whatever failure ends the upload
/// carries the message <c>Received too short packet</c>, since curl keeps the first
/// failure it noted (measured for exits 7, 28 and 55). Neither moves the next scheduled re-send. Before the
/// first block has gone, what curl re-sends is the write request's first four bytes, and
/// that is what is sent. The first datagram received pins the server's endpoint; one from
/// anywhere else ends the upload with exit 56 (<see cref="CurlExitCode.RecvError" />).
/// An option acknowledgement after the first block has gone is taken as curl 8.21.0
/// takes it: its <c>blksize</c> comes into force and the block count restarts, so the
/// next DATA packet is block 1 again, carrying the upload's next bytes at the new size
/// (an empty block 1 when the last block had already gone). As curl does
/// (<see cref="TftpUnexpectedOpcode" />), opcode 0 or 7 as the first reply re-sends the
/// request, a DATA packet as the first reply turns the upload into a download
/// (<see cref="TftpHandOver" />), and any other opcode the upload does not handle ends it
/// with exit 71; once the server has answered, opcode 7 is taken as a timeout and any other
/// is noted while the upload waits on for its ACK.
/// </para>
/// </remarks>
internal sealed class TftpUpload(ITransferContext context, IDatagramChannel channel, Stream upload, long startTimestamp)
{
    /// <summary>
    /// The message curl notes on a datagram under four bytes, and keeps as the failure's
    /// message whatever later ends the transfer.
    /// </summary>
    private const string TooShortMessage = "Received too short packet";

    private readonly byte[] receiveBuffer =
        new byte[TftpPackets.MaximumBlockSize + TftpPackets.DataHeaderLength];

    private readonly TftpTimeLimits limits = new(context, startTimestamp);

    private readonly TftpTransferEvents events = new(context.Events);

    private int blockSize = TftpPackets.DefaultBlockSize;
    private ushort lastSentBlock;
    private bool lastBlockSent;
    private long bytesTransferred;
    private EndPoint? pinnedEndPoint;
    private bool answered;
    private string? notedFailure;
    private TftpRetrySchedule schedule;
    private int retries;
    private TimeSpan resendAt;
    private byte[] lastPacket = [];
    private EndPoint lastDestination = channel.ServerEndPoint;

    /// <summary>
    /// Runs the upload to its end.
    /// </summary>
    /// <param name="file">The file to write and the mode to write it in, from the URL path.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal async ValueTask<TransferResult> RunAsync(TftpRequestFile file)
    {
        schedule = limits.RequestSchedule();
        events.TimeoutsSet(TftpTransferEvents.StartState, schedule);
        var transferSize = upload.CanSeek ? upload.Length - upload.Position : 0;
        if (file.TryBuildRequest(
                (name, mode) => TftpPackets.BuildWriteRequest(
                    name, mode, transferSize, TftpPackets.RequestedBlockSize(context), schedule.RetrySeconds),
                events,
                out byte[] request) is { } refused)
        {
            return refused;
        }

        await SendAsync(request, channel.ServerEndPoint).ConfigureAwait(false);
        retries = 1;
        return await AnswerUntilEndAsync(null).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes over a download whose first reply was an ACK, as curl 8.21.0's
    /// <c>tftp_send_first</c> connects for transmit on an ACK whatever the request asked:
    /// the reply is answered as an upload's first ACK, and the transfer runs on as an upload
    /// of the stream given, which for a download is empty.
    /// </summary>
    /// <param name="handOver">The download's state and its first reply.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal ValueTask<TransferResult> TakeOverAsync(TftpHandOver handOver)
    {
        schedule = limits.RequestSchedule();
        lastPacket = handOver.Request;
        retries = handOver.Retries;
        resendAt = handOver.ResendAt;
        notedFailure = handOver.NotedFailure;
        handOver.Reply.CopyTo(receiveBuffer, 0);
        return AnswerUntilEndAsync(new DatagramReceived(handOver.Reply.Length, handOver.Source));
    }

    /// <summary>
    /// Answers each datagram, and each silence, until one ends the upload. Once curl has
    /// noted a failure, whatever failure ends the upload carries that message.
    /// </summary>
    /// <param name="pending">A datagram already received and not yet answered, if any.</param>
    /// <returns>The outcome of the transfer.</returns>
    private async ValueTask<TransferResult> AnswerUntilEndAsync(DatagramReceived? pending)
    {
        while (true)
        {
            var received = pending ?? await limits.ReceiveBeforeAsync(channel, receiveBuffer, resendAt).ConfigureAwait(false);
            pending = null;
            var outcome = received is not null
                ? await AnswerAsync(received).ConfigureAwait(false)
                : await AnswerSilenceAsync().ConfigureAwait(false);
            if (outcome is not null)
            {
                return notedFailure is { } noted && !outcome.IsSuccess
                    ? outcome with { ErrorMessage = noted }
                    : outcome;
            }
        }
    }

    /// <summary>
    /// Ends the transfer when the maximum time or the retries have run out, otherwise
    /// re-sends the last packet and sets when the next re-send is due.
    /// </summary>
    /// <returns>The transfer's outcome when the silence ended it, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> AnswerSilenceAsync()
    {
        if (limits.FailureIfMaxTimePassed(0, bytesTransferred) is { } timedOut)
        {
            return timedOut;
        }

        if (answered)
        {
            events.TimedOut(lastSentBlock + 1, retries + 1);
        }

        if (!await ResendAsync().ConfigureAwait(false))
        {
            return RetriesRunOut();
        }

        resendAt = limits.Elapsed() + schedule.ResendInterval;
        return null;
    }

    /// <summary>
    /// Answers one received datagram.
    /// </summary>
    /// <param name="received">The datagram's length and source.</param>
    /// <returns>The transfer's outcome when this datagram ended it, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> AnswerAsync(DatagramReceived received)
    {
        if (ReferenceEquals(received, TftpTimeLimits.RefusedReceive))
        {
            return await AnswerTooShortAsync().ConfigureAwait(false);
        }

        pinnedEndPoint ??= received.RemoteEndPoint;
        if (!pinnedEndPoint.Equals(received.RemoteEndPoint))
        {
            return TransferResult.Failure(CurlExitCode.RecvError, "Data received from another address", bytesTransferred);
        }

        if (received.Length < TftpPackets.DataHeaderLength)
        {
            return await AnswerTooShortAsync().ConfigureAwait(false);
        }

        return await AnswerPacketAsync(received).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers one received packet of at least four bytes by its opcode.
    /// </summary>
    /// <param name="received">The packet's length and source.</param>
    /// <returns>The transfer's outcome when this packet ended it, otherwise <see langword="null" />.</returns>
    private ValueTask<TransferResult?> AnswerPacketAsync(DatagramReceived received) =>
        TftpPackets.ReadField(receiveBuffer, 0) switch
        {
            TftpPackets.AcknowledgementOpcode =>
                AcceptAcknowledgementAsync(TftpPackets.ReadField(receiveBuffer, 2), received.RemoteEndPoint),
            TftpPackets.ErrorOpcode => ValueTask.FromResult<TransferResult?>(FailWithErrorPacket(received)),
            TftpPackets.OptionAcknowledgementOpcode => AcceptOptionAcknowledgementAsync(received),
            var opcode when answered => AnswerUnexpectedOpcodeAsync(opcode),
            var opcode => AnswerUnexpectedFirstReplyAsync(opcode, received),
        };

    /// <summary>
    /// Reports an ERROR packet and ends the upload with curl's exit code for it.
    /// </summary>
    /// <param name="received">The ERROR datagram's length and source.</param>
    /// <returns>The failure the packet's error code maps to.</returns>
    private TransferResult FailWithErrorPacket(DatagramReceived received)
    {
        events.ErrorPacket(receiveBuffer.AsSpan(0, received.Length));
        return TftpErrorMapping.ToTransferResult(TftpPackets.ReadField(receiveBuffer, 2));
    }

    /// <summary>
    /// Answers a first reply an upload does not handle as curl does
    /// (<see cref="TftpUnexpectedOpcode" />): a DATA packet hands the transfer over to a
    /// download (<see cref="TftpDownload.TakeOverAsync" />), opcode 0 or 7 notes
    /// <c>Internal error: Unexpected packet</c> and re-sends the request, and any other
    /// opcode ends the upload with exit 71 after <c>tftp_send_first: internal error</c>.
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <param name="received">The packet's length and source.</param>
    /// <returns>The transfer's outcome when the packet ended it, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> AnswerUnexpectedFirstReplyAsync(ushort opcode, DatagramReceived received)
    {
        if (opcode == TftpPackets.DataOpcode)
        {
            var handOver = new TftpHandOver(
                lastPacket, retries, resendAt, notedFailure, receiveBuffer[..received.Length], received.RemoteEndPoint);
            return await new TftpDownload(context, channel, startTimestamp).TakeOverAsync(handOver).ConfigureAwait(false);
        }

        if (TftpUnexpectedOpcode.ResendsTheRequest(opcode))
        {
            NoteFailure(TftpUnexpectedOpcode.UnexpectedPacketMessage);
            return await ResendOrRunOutAsync().ConfigureAwait(false);
        }

        events.InternalError(TftpUnexpectedOpcode.SendFirstMessage);
        return TransferResult.Failure(CurlExitCode.TftpIllegal, TftpUnexpectedOpcode.UnexpectedPacketMessage, bytesTransferred);
    }

    /// <summary>
    /// Answers an opcode an upload that has started does not handle as curl does
    /// (<see cref="TftpUnexpectedOpcode" />): it notes
    /// <c>Internal error: Unexpected packet</c> for an opcode curl does not know; opcode 7
    /// is then taken as a timeout, re-sending the last block, and any other opcode notes
    /// <c>tftp_tx: internal error, event: N</c> and waits on.
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <returns>The failure when a timeout found the retries run out, otherwise <see langword="null" />.</returns>
    private ValueTask<TransferResult?> AnswerUnexpectedOpcodeAsync(ushort opcode)
    {
        if (TftpUnexpectedOpcode.IsUnknown(opcode))
        {
            NoteFailure(TftpUnexpectedOpcode.UnexpectedPacketMessage);
        }

        if (opcode == TftpUnexpectedOpcode.TimeoutEventOpcode)
        {
            events.TimedOut(lastSentBlock + 1, retries + 1);
            return ResendOrRunOutAsync();
        }

        NoteFailure(string.Create(CultureInfo.InvariantCulture, $"tftp_tx: internal error, event: {opcode}"));
        return ValueTask.FromResult<TransferResult?>(null);
    }

    /// <summary>
    /// Answers a datagram under four bytes, or a receive the server's port refused, as curl
    /// does: notes <c>Received too short packet</c>, and re-sends the last packet at once
    /// without moving the next scheduled re-send.
    /// </summary>
    /// <returns>The failure when the retries had already run out, otherwise <see langword="null" />.</returns>
    private ValueTask<TransferResult?> AnswerTooShortAsync()
    {
        NoteFailure(TooShortMessage);
        return ResendOrRunOutAsync();
    }

    /// <summary>
    /// Notes a failure message as curl's <c>failf</c> does: reported at once, and kept as
    /// the message of whatever failure ends the upload unless an earlier one was noted.
    /// </summary>
    /// <param name="message">The message.</param>
    private void NoteFailure(string message)
    {
        notedFailure ??= message;
        events.InternalError(message);
    }

    /// <summary>
    /// Re-sends the last packet at once and counts a retry, without moving the next
    /// scheduled re-send, or ends the upload when the retries have already run out.
    /// </summary>
    /// <returns>The failure when the retries had already run out, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> ResendOrRunOutAsync() =>
        await ResendAsync().ConfigureAwait(false) ? null : RetriesRunOut();

    /// <summary>
    /// Takes an OACK as the acknowledgement of block 0 with the block size it grants,
    /// restarting the block count, or ends the upload with exit 71 and sends nothing when
    /// curl rejects the OACK. An upload ignores <c>tsize</c>.
    /// </summary>
    /// <param name="received">The OACK datagram's length and source.</param>
    /// <returns>
    /// The failure when the OACK is rejected, otherwise what
    /// <see cref="AcceptAcknowledgementAsync" /> returns for block 0.
    /// </returns>
    private async ValueTask<TransferResult?> AcceptOptionAcknowledgementAsync(DatagramReceived received)
    {
        var requestedBlockSize = TftpPackets.RequestedBlockSize(context) ?? TftpPackets.DefaultBlockSize;
        var acknowledgement = TftpOptionAcknowledgement.Parse(
            receiveBuffer.AsSpan(2, received.Length - 2),
            requestedBlockSize,
            isDownload: false);
        events.OptionsAcknowledged(acknowledgement.Options, requestedBlockSize);
        if (acknowledgement.Failure is { } failure)
        {
            return TransferResult.Failure(CurlExitCode.TftpIllegal, failure, bytesTransferred);
        }

        blockSize = acknowledgement.BlockSize;
        lastSentBlock = 0;
        lastBlockSent = false;
        return await AcceptAcknowledgementAsync(0, received.RemoteEndPoint).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers the acknowledgement of <paramref name="block" />: the first one re-derives
    /// the schedule from the maximum time left; one of a block other than the one last sent
    /// (see <see cref="IsExpectedAcknowledgement" />) re-sends it; one of the last block ends the transfer; any other sends the next block
    /// to the endpoint the acknowledgement came from, which is the server's transfer
    /// identifier (RFC 1350 section 4).
    /// </summary>
    /// <param name="block">The block acknowledged; 0 acknowledges the write request.</param>
    /// <param name="source">The endpoint the acknowledgement came from.</param>
    /// <returns>
    /// A success when the last block is acknowledged, a failure when a wrong
    /// acknowledgement uses up the retries, otherwise <see langword="null" />.
    /// </returns>
    private async ValueTask<TransferResult?> AcceptAcknowledgementAsync(ushort block, EndPoint source)
    {
        if (!answered)
        {
            answered = true;
            schedule = limits.AnsweredSchedule();
            events.Answered(isDownload: false, schedule);
            lastPacket = lastPacket[..TftpPackets.DataHeaderLength];
        }

        if (!IsExpectedAcknowledgement(block))
        {
            events.UnexpectedAcknowledgement(block, lastSentBlock);
            return await ResendAsync().ConfigureAwait(false)
                ? null
                : TransferResult.Failure(
                    CurlExitCode.SendError,
                    string.Create(CultureInfo.InvariantCulture, $"tftp_tx: giving up waiting for block {lastSentBlock} ack"),
                    bytesTransferred);
        }

        if (lastBlockSent)
        {
            return TransferResult.Success(bytesTransferred);
        }

        retries = 0;
        await SendNextBlockAsync(source).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Tells whether <paramref name="block" /> acknowledges the block last sent. As curl
    /// 8.21.0's <c>tftp_tx</c> does for a tftpd-hpa bug, an ACK of 65535 also counts while
    /// block 0 is awaited: the write request's own ACK, and the ACK after the block number
    /// wraps from 65535 to 0.
    /// </summary>
    /// <param name="block">The block acknowledged.</param>
    /// <returns><see langword="true" /> when the acknowledgement is the one awaited.</returns>
    private bool IsExpectedAcknowledgement(ushort block) =>
        block == lastSentBlock || (lastSentBlock == 0 && block == ushort.MaxValue);

    /// <summary>
    /// Re-sends the last packet and counts a retry, unless the retries have run out.
    /// </summary>
    /// <returns><see langword="false" /> when the retries had run out and nothing was sent.</returns>
    private async ValueTask<bool> ResendAsync()
    {
        if (retries >= schedule.RetryLimit)
        {
            return false;
        }

        retries++;
        await channel.SendAsync(lastPacket, lastDestination, context.CancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Gets the failure the upload ends with when its retries run out: exit 7 before the
    /// server has answered, exit 28 after.
    /// </summary>
    /// <returns>The failure.</returns>
    private TransferResult RetriesRunOut() =>
        answered
            ? TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached", bytesTransferred)
            : TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect to server", bytesTransferred);

    /// <summary>
    /// Reads up to one block from the upload and sends it as the next DATA packet.
    /// </summary>
    /// <param name="destination">The server's transfer identifier.</param>
    /// <returns>A task that completes when the block has been sent.</returns>
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
        lastBlockSent = filled < blockSize;
        bytesTransferred += filled;

        await SendAsync(TftpPackets.BuildData(lastSentBlock, payload.AsSpan(0, filled)), destination)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a packet, remembers it for re-sending and sets when the next re-send is due.
    /// </summary>
    /// <param name="packet">The whole datagram.</param>
    /// <param name="destination">Where it goes.</param>
    /// <returns>A task that completes when the packet has been sent.</returns>
    private async ValueTask SendAsync(byte[] packet, EndPoint destination)
    {
        await channel.SendAsync(packet, destination, context.CancellationToken).ConfigureAwait(false);
        lastPacket = packet;
        lastDestination = destination;
        resendAt = limits.Elapsed() + schedule.ResendInterval;
    }
}
