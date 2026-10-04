using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// One TFTP download over an open channel: sends the read request, then answers each
/// packet until the last block or an ERROR packet ends the transfer, re-sending its last
/// packet to a silent server on curl 8.21.0's schedule until the retries or the time run
/// out.
/// </summary>
/// <param name="context">The transfer being performed.</param>
/// <param name="channel">The open channel, which this class does not dispose.</param>
/// <param name="startTimestamp">
/// The <see cref="ITransferContext.TimeProvider" /> timestamp the transfer started at,
/// which <see cref="ITransferContext.MaxTime" /> and the connect timeout count from.
/// </param>
/// <remarks>
/// <para>
/// The first datagram received pins the server's endpoint; a later one from anywhere else
/// ends the transfer with exit 56 (<see cref="CurlExitCode.RecvError" />) and sends the
/// stranger nothing, as curl does. A repeat of the last block is acknowledged again and
/// not written twice. Any other unexpected block is ignored, and the download waits on. As
/// curl does (<see cref="TftpUnexpectedOpcode" />), opcode 0 or 7 as the first reply
/// re-sends the request, opcode 7 later is taken as a timeout, an ACK as the first reply
/// turns the download into an upload of nothing (<see cref="TftpHandOver" />), and any other
/// opcode a download does not handle ends it with exit 71.
/// </para>
/// <para>
/// A datagram shorter than four bytes re-sends the last packet (the read request or the
/// last ACK) at once and counts a retry, without moving the next scheduled re-send; when
/// the retries have already run out it ends the download as silence would. From then on
/// whatever failure ends the download carries the message <c>Received too short packet</c>,
/// since curl keeps the first failure it noted (measured for exits 7, 28, 56 and 68).
/// </para>
/// <para>
/// Every wait goes through <see cref="ITransferContext.TimeProvider" />. The schedule
/// (<see cref="TftpRetrySchedule" />) comes from the time left at the start - the connect
/// timeout, 300 seconds by default, or <see cref="ITransferContext.MaxTime" /> when that
/// is sooner - and again from the time <see cref="ITransferContext.MaxTime" /> leaves when
/// the first DATA or OACK arrives. An unanswered read request ends with exit 7
/// (<see cref="CurlExitCode.CouldntConnect" />) and a server silent mid-transfer with
/// exit 28 (<see cref="CurlExitCode.OperationTimedOut" />) <c>Timeout was reached</c>;
/// <see cref="ITransferContext.MaxTime" /> passing ends it with exit 28 and the elapsed
/// milliseconds and bytes received.
/// </para>
/// <para>
/// Each block goes through curl's download writer: <see cref="ITransferContext.NoBody" />
/// (<c>-I</c>) ends the download at the first DATA block with exit 8 and writes nothing,
/// and <see cref="ITransferContext.MaxFileSize" /> ends it with exit 63 once a block passes
/// the limit, after writing the bytes under it; either way the server is sent a bare ERROR
/// packet. An upload ignores both.
/// </para>
/// </remarks>
internal sealed class TftpDownload(ITransferContext context, IDatagramChannel channel, long startTimestamp)
{
    /// <summary>
    /// The message curl notes on a datagram under four bytes, and keeps as the failure's
    /// message whatever later ends the transfer.
    /// </summary>
    private const string TooShortMessage = "Received too short packet";

    private readonly byte[] buffer =
        new byte[TftpPackets.MaximumBlockSize + TftpPackets.DataHeaderLength];

    private readonly TftpTimeLimits limits = new(context, startTimestamp);

    private readonly TftpTransferLog log = new(context.DiagnosticLog);

    private readonly TftpTransferEvents events = new(context.Events);

    private int blockSize = TftpPackets.DefaultBlockSize;
    private ushort expectedBlock = 1;
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
    /// Runs the download to its end.
    /// </summary>
    /// <param name="file">The file to read and the mode to read it in, from the URL path.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal async ValueTask<TransferResult> RunAsync(TftpRequestFile file)
    {
        schedule = limits.RequestSchedule();
        events.TimeoutsSet(TftpTransferEvents.StartState, schedule);
        int? requestedBlockSize = TftpPackets.RequestedBlockSize(context);
        if (file.TryBuildRequest(
                (name, mode) => TftpPackets.BuildReadRequest(name, mode, requestedBlockSize, schedule.RetrySeconds),
                events,
                out byte[] request) is { } refused)
        {
            return refused;
        }

        await SendAsync(request, channel.ServerEndPoint).ConfigureAwait(false);
        log.RequestSent("read", file.LoggedName, requestedBlockSize, schedule.RetrySeconds);
        retries = 1;
        return await AnswerUntilEndAsync(null).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes over an upload whose first reply was a DATA packet, as curl 8.21.0's
    /// <c>tftp_send_first</c> connects for receive on DATA whatever the request asked: the
    /// reply is answered as a download's first DATA block, written to the output and
    /// acknowledged, and the transfer runs on as a download.
    /// </summary>
    /// <param name="handOver">The upload's state and its first reply.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal ValueTask<TransferResult> TakeOverAsync(TftpHandOver handOver)
    {
        schedule = limits.RequestSchedule();
        lastPacket = handOver.Request;
        retries = handOver.Retries;
        resendAt = handOver.ResendAt;
        notedFailure = handOver.NotedFailure;
        handOver.Reply.CopyTo(buffer, 0);
        return AnswerUntilEndAsync(new DatagramReceived(handOver.Reply.Length, handOver.Source));
    }

    /// <summary>
    /// Answers each datagram, and each silence, until one ends the download. Once curl has
    /// noted a failure, whatever failure ends the download carries that message.
    /// </summary>
    /// <param name="pending">A datagram already received and not yet answered, if any.</param>
    /// <returns>The outcome of the transfer.</returns>
    private async ValueTask<TransferResult> AnswerUntilEndAsync(DatagramReceived? pending)
    {
        while (true)
        {
            var received = pending ?? await limits.ReceiveBeforeAsync(channel, buffer, resendAt).ConfigureAwait(false);
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
        if (limits.FailureIfMaxTimePassed(bytesTransferred, bytesTransferred) is { } timedOut)
        {
            return timedOut;
        }

        if (answered)
        {
            events.TimedOut(expectedBlock, retries + 1);
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
    private ValueTask<TransferResult?> AnswerAsync(DatagramReceived received)
    {
        if (IsFromStranger(received.RemoteEndPoint))
        {
            return ValueTask.FromResult<TransferResult?>(
                TransferResult.Failure(CurlExitCode.RecvError, "Data received from another address", bytesTransferred));
        }

        if (received.Length < TftpPackets.DataHeaderLength)
        {
            return AnswerTooShortAsync();
        }

        log.Received(buffer.AsSpan(0, received.Length));
        return TftpPackets.ReadField(buffer, 0) switch
        {
            TftpPackets.DataOpcode => AcceptDataAsync(received),
            TftpPackets.ErrorOpcode => ValueTask.FromResult<TransferResult?>(FailWithErrorPacket(received)),
            TftpPackets.OptionAcknowledgementOpcode => AcceptOptionAcknowledgementAsync(received),
            var opcode => AnswerOtherOpcodeAsync(opcode, received),
        };
    }

    /// <summary>
    /// Answers an opcode other than DATA, ERROR and OACK, as a first reply
    /// (<see cref="AnswerUnexpectedFirstReplyAsync" />) or once the download has started
    /// (<see cref="AnswerUnexpectedOpcodeAsync" />).
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <param name="received">The packet's length and source.</param>
    /// <returns>The transfer's outcome when the packet ended it, otherwise <see langword="null" />.</returns>
    private ValueTask<TransferResult?> AnswerOtherOpcodeAsync(ushort opcode, DatagramReceived received) =>
        answered ? AnswerUnexpectedOpcodeAsync(opcode) : AnswerUnexpectedFirstReplyAsync(opcode, received);

    /// <summary>
    /// Answers a first reply a download does not handle as curl does
    /// (<see cref="TftpUnexpectedOpcode" />): an ACK hands the transfer over to an upload of
    /// nothing (<see cref="TftpUpload.TakeOverAsync" />), opcode 0 or 7 notes
    /// <c>Internal error: Unexpected packet</c> and re-sends the request, and any other
    /// opcode ends the download with exit 71 after <c>tftp_send_first: internal error</c>.
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <param name="received">The packet's length and source.</param>
    /// <returns>The transfer's outcome when the packet ended it, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> AnswerUnexpectedFirstReplyAsync(ushort opcode, DatagramReceived received)
    {
        if (opcode == TftpPackets.AcknowledgementOpcode)
        {
            var handOver = new TftpHandOver(
                lastPacket, retries, resendAt, notedFailure, buffer[..received.Length], received.RemoteEndPoint);
            return await new TftpUpload(context, channel, Stream.Null, startTimestamp)
                .TakeOverAsync(handOver)
                .ConfigureAwait(false);
        }

        if (TftpUnexpectedOpcode.ResendsTheRequest(opcode))
        {
            NoteUnexpectedPacket();
            return await ResendOrRunOutAsync().ConfigureAwait(false);
        }

        events.InternalError(TftpUnexpectedOpcode.SendFirstMessage);
        return TransferResult.Failure(CurlExitCode.TftpIllegal, TftpUnexpectedOpcode.UnexpectedPacketMessage, bytesTransferred);
    }

    /// <summary>
    /// Answers an opcode a download that has started does not handle as curl does
    /// (<see cref="TftpUnexpectedOpcode" />): opcode 7 notes
    /// <c>Internal error: Unexpected packet</c> and is taken as a timeout, re-sending the
    /// last ACK; an ACK ends the download with exit 71 <c>tftp_rx: internal error</c>, and
    /// any other opcode notes <c>Internal error: Unexpected packet</c> first and ends it with
    /// that message, sending nothing.
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <returns>The transfer's outcome when the packet ended it, otherwise <see langword="null" />.</returns>
    private ValueTask<TransferResult?> AnswerUnexpectedOpcodeAsync(ushort opcode)
    {
        if (opcode == TftpUnexpectedOpcode.TimeoutEventOpcode)
        {
            NoteUnexpectedPacket();
            events.TimedOut(expectedBlock, retries + 1);
            return ResendOrRunOutAsync();
        }

        var message = TftpUnexpectedOpcode.ReceiveMessage;
        if (TftpUnexpectedOpcode.IsUnknown(opcode))
        {
            message = TftpUnexpectedOpcode.UnexpectedPacketMessage;
            events.InternalError(message);
        }

        events.InternalError(TftpUnexpectedOpcode.ReceiveMessage);
        return ValueTask.FromResult<TransferResult?>(
            TransferResult.Failure(CurlExitCode.TftpIllegal, message, bytesTransferred));
    }

    /// <summary>
    /// Notes <c>Internal error: Unexpected packet</c> as curl does, keeping it as the message
    /// of whatever failure ends the download unless an earlier one was noted.
    /// </summary>
    private void NoteUnexpectedPacket()
    {
        notedFailure ??= TftpUnexpectedOpcode.UnexpectedPacketMessage;
        events.InternalError(TftpUnexpectedOpcode.UnexpectedPacketMessage);
    }

    /// <summary>
    /// Pins the server's endpoint to the source of the first datagram received, and tells
    /// whether a datagram came from anywhere else.
    /// </summary>
    /// <param name="source">Where the datagram came from.</param>
    /// <returns><see langword="true" /> when <paramref name="source" /> is not the pinned endpoint.</returns>
    private bool IsFromStranger(EndPoint source)
    {
        pinnedEndPoint ??= source;
        return !pinnedEndPoint.Equals(source);
    }

    /// <summary>
    /// Logs and reports an ERROR packet and ends the download with curl's exit code for it.
    /// </summary>
    /// <param name="received">The ERROR datagram's length and source.</param>
    /// <returns>The failure the packet's error code maps to.</returns>
    private TransferResult FailWithErrorPacket(DatagramReceived received)
    {
        log.ErrorPacket(buffer.AsSpan(0, received.Length));
        events.ErrorPacket(buffer.AsSpan(0, received.Length));
        return TftpErrorMapping.ToTransferResult(TftpPackets.ReadField(buffer, 2));
    }

    /// <summary>
    /// Takes the block size an OACK grants and acknowledges it as block 0, or ends the
    /// download with exit 71 and sends nothing when curl rejects the OACK.
    /// </summary>
    /// <param name="received">The OACK datagram's length and source.</param>
    /// <returns>The failure when the OACK is rejected, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> AcceptOptionAcknowledgementAsync(DatagramReceived received)
    {
        var body = buffer.AsSpan(2, received.Length - 2);
        var requestedBlockSize = TftpPackets.RequestedBlockSize(context);
        var acknowledgement = TftpOptionAcknowledgement.Parse(
            body,
            requestedBlockSize ?? TftpPackets.DefaultBlockSize,
            isDownload: true);
        events.OptionsAcknowledged(acknowledgement.Options, requestedBlockSize ?? TftpPackets.DefaultBlockSize);
        if (acknowledgement.Failure is { } failure)
        {
            return TransferResult.Failure(CurlExitCode.TftpIllegal, failure, bytesTransferred);
        }

        blockSize = acknowledgement.BlockSize;
        log.OptionsAgreed(body, requestedBlockSize, blockSize);
        await AcknowledgeNewAsync(0, received.RemoteEndPoint).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Writes the expected DATA block to the output and acknowledges it to the endpoint it
    /// came from, which is the server's transfer identifier (RFC 1350 section 4), or
    /// acknowledges a repeat of the last block again without writing it. When the download
    /// writer stops the download (<see cref="WriteBlockAsync" />), it sends that endpoint a
    /// bare ERROR packet carrying the last block acknowledged instead, as curl does.
    /// </summary>
    /// <param name="received">The DATA datagram's length and source.</param>
    /// <returns>
    /// A success when the block is shorter than the block size and so the last, the
    /// writer's failure when it stopped the download, otherwise <see langword="null" />.
    /// </returns>
    private async ValueTask<TransferResult?> AcceptDataAsync(DatagramReceived received)
    {
        var block = TftpPackets.ReadField(buffer, 2);
        var payloadLength = received.Length - TftpPackets.DataHeaderLength;
        if (block == expectedBlock)
        {
            if (await WriteBlockAsync(payloadLength).ConfigureAwait(false) is { } stopped)
            {
                var lastAcknowledged = unchecked((ushort)(expectedBlock - 1));
                await channel
                    .SendAsync(TftpPackets.BuildAbandonment(lastAcknowledged), received.RemoteEndPoint, context.CancellationToken)
                    .ConfigureAwait(false);
                return stopped;
            }

            expectedBlock = unchecked((ushort)(block + 1));
            await AcknowledgeNewAsync(block, received.RemoteEndPoint).ConfigureAwait(false);
        }
        else if (block == unchecked((ushort)(expectedBlock - 1)))
        {
            events.RepeatedData(block);
            await SendAsync(TftpPackets.BuildAcknowledgement(block), received.RemoteEndPoint).ConfigureAwait(false);
        }
        else
        {
            events.UnexpectedData(block, expectedBlock);
            return null;
        }

        return payloadLength < blockSize ? TransferResult.Success(bytesTransferred) : null;
    }

    /// <summary>
    /// Hands the expected block's payload to the output as curl 8.21.0's download writer
    /// (<c>cw_download_write</c>) does: under <see cref="ITransferContext.NoBody" /> it writes
    /// nothing and stops with exit 8 <c>Weird server reply</c>; with a
    /// <see cref="ITransferContext.MaxFileSize" /> above 0 it writes only the bytes left under
    /// the limit and stops with exit 63 when it cut any. The payload is reported as data
    /// received either way.
    /// </summary>
    /// <param name="payloadLength">The block's payload length, after the DATA header.</param>
    /// <returns>The failure when the writer stopped the download, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> WriteBlockAsync(int payloadLength)
    {
        var payload = buffer.AsMemory(TftpPackets.DataHeaderLength, payloadLength);
        if (context.NoBody)
        {
            events.DataReceived(payload.Span);
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply", bytesTransferred);
        }

        var limit = context.MaxFileSize is > 0 and long maxFileSize ? maxFileSize : long.MaxValue;
        var allowed = (int)Math.Min(payloadLength, limit - bytesTransferred);
        await context.Output.WriteAsync(payload[..allowed], context.CancellationToken).ConfigureAwait(false);
        events.DataReceived(payload.Span);
        bytesTransferred += allowed;
        if (allowed == payloadLength)
        {
            return null;
        }

        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"Exceeded the maximum allowed file size ({limit}) with {bytesTransferred} bytes");
        events.MaxFileSizeExceeded(message);
        return TransferResult.Failure(CurlExitCode.FilesizeExceeded, message, bytesTransferred);
    }

    /// <summary>
    /// Acknowledges a block, or an OACK as block 0, that moved the transfer on: the first
    /// one re-derives the schedule from the maximum time left, and each resets the retries
    /// so the acknowledgement may be re-sent <see cref="TftpRetrySchedule.RetryLimit" /> times.
    /// </summary>
    /// <param name="block">The block acknowledged.</param>
    /// <param name="destination">The server's transfer identifier.</param>
    /// <returns>A task that completes when the acknowledgement has been sent.</returns>
    private ValueTask AcknowledgeNewAsync(ushort block, EndPoint destination)
    {
        if (!answered)
        {
            answered = true;
            schedule = limits.AnsweredSchedule();
            events.Answered(isDownload: true, schedule);
        }

        retries = 0;
        return SendAsync(TftpPackets.BuildAcknowledgement(block), destination);
    }

    /// <summary>
    /// Answers a datagram under four bytes as curl does: notes it, so the failure that ends
    /// the download carries its message, and re-sends the last packet at once without
    /// moving the next scheduled re-send.
    /// </summary>
    /// <returns>The failure when the retries had already run out, otherwise <see langword="null" />.</returns>
    private ValueTask<TransferResult?> AnswerTooShortAsync()
    {
        notedFailure ??= TooShortMessage;
        return ResendOrRunOutAsync();
    }

    /// <summary>
    /// Re-sends the last packet at once and counts a retry, without moving the next
    /// scheduled re-send, or ends the download when the retries have already run out.
    /// </summary>
    /// <returns>The failure when the retries had already run out, otherwise <see langword="null" />.</returns>
    private async ValueTask<TransferResult?> ResendOrRunOutAsync() =>
        await ResendAsync().ConfigureAwait(false) ? null : RetriesRunOut();

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
        log.Retransmitting(retries, schedule.RetryLimit);
        await channel.SendAsync(lastPacket, lastDestination, context.CancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Gets the failure the download ends with when its retries run out: exit 7 before the
    /// server has answered, exit 28 after.
    /// </summary>
    /// <returns>The failure.</returns>
    private TransferResult RetriesRunOut() =>
        answered
            ? TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached", bytesTransferred)
            : TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect to server");

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
