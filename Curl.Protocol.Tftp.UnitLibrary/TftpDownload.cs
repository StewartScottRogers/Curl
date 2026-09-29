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
/// not written twice. Any other unexpected block and an opcode a download never receives
/// are ignored, and the download waits on.
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
    private bool tooShortReceived;
    private TftpRetrySchedule schedule;
    private int retries;
    private TimeSpan resendAt;
    private byte[] lastPacket = [];
    private EndPoint lastDestination = channel.ServerEndPoint;

    /// <summary>
    /// Runs the download to its end.
    /// </summary>
    /// <param name="fileName">The file to read, decoded from the URL path.</param>
    /// <returns>The outcome of the transfer.</returns>
    internal async ValueTask<TransferResult> RunAsync(string fileName)
    {
        schedule = limits.RequestSchedule();
        events.TimeoutsSet(TftpTransferEvents.StartState, schedule);
        int? requestedBlockSize = TftpPackets.RequestedBlockSize(context);
        await SendAsync(
                TftpPackets.BuildReadRequest(fileName, requestedBlockSize, schedule.RetrySeconds),
                channel.ServerEndPoint)
            .ConfigureAwait(false);
        log.RequestSent("read", fileName, requestedBlockSize, schedule.RetrySeconds);
        retries = 1;

        while (true)
        {
            var outcome = await limits.ReceiveBeforeAsync(channel, buffer, resendAt).ConfigureAwait(false) is { } received
                ? await AnswerAsync(received).ConfigureAwait(false)
                : await AnswerSilenceAsync().ConfigureAwait(false);
            if (outcome is not null)
            {
                return tooShortReceived && !outcome.IsSuccess
                    ? outcome with { ErrorMessage = TooShortMessage }
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
            _ => ValueTask.FromResult<TransferResult?>(null),
        };
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
    /// Takes the block size an OACK grants and acknowledges it as block 0.
    /// </summary>
    /// <param name="received">The OACK datagram's length and source.</param>
    /// <returns><see langword="null" />, since an OACK never ends the transfer.</returns>
    private TransferResult FailWithErrorPacket(DatagramReceived received)
    {
        log.ErrorPacket(buffer.AsSpan(0, received.Length));
        events.ErrorPacket(buffer.AsSpan(0, received.Length));
        return TftpErrorMapping.ToTransferResult(TftpPackets.ReadField(buffer, 2));
    }

    private async ValueTask<TransferResult?> AcceptOptionAcknowledgementAsync(DatagramReceived received)
    {
        blockSize = TftpPackets.ReadAcknowledgedBlockSize(buffer.AsSpan(2, received.Length - 2));
        log.OptionsAgreed(buffer.AsSpan(2, received.Length - 2), TftpPackets.RequestedBlockSize(context), blockSize);
        events.OptionsAcknowledged(
            buffer.AsSpan(2, received.Length - 2),
            isDownload: true,
            TftpPackets.RequestedBlockSize(context) ?? TftpPackets.DefaultBlockSize);
        await AcknowledgeNewAsync(0, received.RemoteEndPoint).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Writes the expected DATA block to the output and acknowledges it to the endpoint it
    /// came from, which is the server's transfer identifier (RFC 1350 section 4), or
    /// acknowledges a repeat of the last block again without writing it.
    /// </summary>
    /// <param name="received">The DATA datagram's length and source.</param>
    /// <returns>
    /// A success when the block is shorter than the block size and so the last, otherwise
    /// <see langword="null" />.
    /// </returns>
    private async ValueTask<TransferResult?> AcceptDataAsync(DatagramReceived received)
    {
        var block = TftpPackets.ReadField(buffer, 2);
        var payloadLength = received.Length - TftpPackets.DataHeaderLength;
        if (block == expectedBlock)
        {
            await context.Output
                .WriteAsync(buffer.AsMemory(TftpPackets.DataHeaderLength, payloadLength), context.CancellationToken)
                .ConfigureAwait(false);
            events.DataReceived(buffer.AsSpan(TftpPackets.DataHeaderLength, payloadLength));
            bytesTransferred += payloadLength;
            expectedBlock = unchecked((ushort)(block + 1));
            await AcknowledgeNewAsync(block, received.RemoteEndPoint).ConfigureAwait(false);
        }
        else if (block == unchecked((ushort)(expectedBlock - 1)))
        {
            await SendAsync(TftpPackets.BuildAcknowledgement(block), received.RemoteEndPoint).ConfigureAwait(false);
        }
        else
        {
            return null;
        }

        return payloadLength < blockSize ? TransferResult.Success(bytesTransferred) : null;
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
    private async ValueTask<TransferResult?> AnswerTooShortAsync()
    {
        tooShortReceived = true;
        return await ResendAsync().ConfigureAwait(false) ? null : RetriesRunOut();
    }

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
