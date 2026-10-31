using System.Text;
using Curl.Extended.Protocol.Coap.Blockwise;
using Curl.Extended.Protocol.Coap.Client;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Observe;

/// <summary>
/// Follows one observation once the server has answered its registration with a notification (RFC 7641,
/// ADR-1064): writes each notification as it arrives, its payload and then a newline, under <c>-i</c> after a
/// <c>CoAP/1.0 c.dd</c> line; drops one older than the last taken (section 3.4); acknowledges a confirmable one
/// (section 3.5); resets any other message (section 3.6); ends after a notification that is not a 2.xx or carries no
/// Observe option (section 3.2); re-registers with a non-confirmable GET carrying Observe 0 and the registration's
/// token when the last notification's Max-Age and a 5-second margin pass without another (section 3.3.1); fetches
/// the later blocks of a notification carrying Block2 with M set by GETs without Observe, under a token of their
/// own, and writes it once, whole (RFC 7959 section 2.6); and when the transfer is cancelled, at <c>--max-time</c>
/// or by interruption, deregisters with a GET carrying Observe 1 and the registration's token (section 3.6) before ending.
/// </summary>
/// <param name="channel">The channel the registration went over.</param>
/// <param name="timeProvider">The clock the arrival times are read from and the Max-Age waits run on.</param>
internal sealed class CoapObservation(IDatagramChannel channel, TimeProvider timeProvider)
{
    /// <summary>How long past the last notification's Max-Age the observation waits before re-registering.</summary>
    public static readonly TimeSpan ReregistrationMargin = TimeSpan.FromSeconds(5);

    /// <summary>The Max-Age a notification without the option has: 60 seconds (RFC 7252 section 5.10.5).</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(60);

    private const int MaximumDatagramLength = 65535;

    private const int MaximumObserveLength = 3;

    private const int MaximumMaxAgeLength = 4;

    /// <summary>The first retransmission timeout of a block fetch: ACK_TIMEOUT (RFC 7252 section 4.8).</summary>
    private static readonly TimeSpan BlockFetchTimeout = TimeSpan.FromSeconds(2);

    private static readonly byte[] NotificationEnd = "\n"u8.ToArray();

    private uint lastSequence;

    private DateTimeOffset lastArrival;

    private TimeSpan lastMaxAge = DefaultMaxAge;

    private ushort nextMessageId;

    private long written;

    /// <summary>Gives <paramref name="request" /> with an Observe option of 0, registering an observation (section 3.1).</summary>
    /// <param name="request">The request.</param>
    /// <returns>The registration.</returns>
    public static CoapMessage RegistrationOf(CoapMessage request) =>
        request with { Options = [.. request.Options, ObserveOptionOf(CoapObserveValue.Register)] };

    /// <summary>
    /// Tells whether a response is a notification: a 2.xx with an Observe option of at most 3 bytes. A server that
    /// ignores the registration answers without one (section 2).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true" /> for a notification.</returns>
    public static bool IsNotification(CoapMessage response) => response.Code.Class == 2 && SequenceOf(response) is not null;

    /// <summary>Writes <paramref name="first" />, then every later notification, until the observation ends or is cancelled.</summary>
    /// <param name="context">The transfer.</param>
    /// <param name="registration">The registration request.</param>
    /// <param name="first">The notification that answered it.</param>
    /// <returns>The transfer's result once the server ends the observation.</returns>
    /// <exception cref="OperationCanceledException">The transfer was cancelled; the observation was deregistered.</exception>
    public async ValueTask<TransferResult> FollowAsync(ITransferContext context, CoapMessage registration, CoapMessage first)
    {
        nextMessageId = unchecked((ushort)(registration.MessageId + 1));
        OperationCanceledException cancellation;
        try
        {
            await TakeAsync(context, registration, first, SequenceOf(first)!.Value).ConfigureAwait(false);
            await FollowNotificationsAsync(context, registration).ConfigureAwait(false);
            return TransferResult.Success(written);
        }
        catch (OperationCanceledException exception)
        {
            cancellation = exception;
        }

        // Deregisters outside the catch block, so the compiler adds no rethrow branch of its own (RFC 7641 section 3.6).
        await SendAsync(DeregistrationOf(registration) with { MessageId = nextMessageId }, CancellationToken.None).ConfigureAwait(false);
        throw cancellation;
    }

    /// <summary>Gives the token the later blocks of a notification are fetched under: the registration's with its last bit flipped, or 0x01 for an empty one.</summary>
    /// <param name="registrationToken">The registration's token.</param>
    /// <returns>The block fetch token.</returns>
    internal static byte[] BlockFetchTokenOf(byte[] registrationToken) =>
        registrationToken.Length == 0 ? [1] : [.. registrationToken[..^1], (byte)(registrationToken[^1] ^ 1)];

    /// <summary>Gives a notification's Max-Age (section 3.3.1), 60 seconds without a valid Max-Age option.</summary>
    /// <param name="notification">The notification.</param>
    /// <returns>The Max-Age.</returns>
    internal static TimeSpan MaxAgeOf(CoapMessage notification) =>
        notification.Options.FirstOrDefault(option => option.Number == CoapOptionNumber.MaxAge) is { Value.Length: <= MaximumMaxAgeLength } maxAge
            ? TimeSpan.FromSeconds(CoapUnsignedInteger.Decode(maxAge.Value, MaximumMaxAgeLength))
            : DefaultMaxAge;

    /// <summary>Receives and answers notifications, re-registering whenever Max-Age and the margin pass without one, until one ends the observation.</summary>
    private async ValueTask FollowNotificationsAsync(ITransferContext context, CoapMessage registration)
    {
        byte[] buffer = new byte[MaximumDatagramLength];
        Task<DatagramReceived> receive = channel.ReceiveAsync(buffer, context.CancellationToken).AsTask();
        while (true)
        {
            if (!await ArrivesWithinAsync(receive, lastMaxAge + ReregistrationMargin, context.CancellationToken).ConfigureAwait(false))
            {
                await SendAsync(ReregistrationOf(registration), context.CancellationToken).ConfigureAwait(false);
                continue;
            }

            DatagramReceived received = await receive.ConfigureAwait(false);
            if (await AnswerAsync(context, registration, buffer.AsSpan(0, received.Length).ToArray()).ConfigureAwait(false))
            {
                return;
            }

            // Receives the next datagram only now, so none is taken here while a block fetch reads the channel.
            receive = channel.ReceiveAsync(buffer, context.CancellationToken).AsTask();
        }
    }

    /// <summary>Gives <see langword="true" /> when <paramref name="receive" /> completes within <paramref name="wait" />.</summary>
    private async ValueTask<bool> ArrivesWithinAsync(Task<DatagramReceived> receive, TimeSpan wait, CancellationToken stop)
    {
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(stop);
        Task delay = Task.Delay(wait, timeProvider, expiry.Token);
        Task arrived = await Task.WhenAny(receive, delay).ConfigureAwait(false);
        await expiry.CancelAsync().ConfigureAwait(false);
        stop.ThrowIfCancellationRequested();
        return arrived == receive;
    }

    /// <summary>The registration again, non-confirmable under the next Message ID, its token and Observe 0 kept (section 3.3.1).</summary>
    private CoapMessage ReregistrationOf(CoapMessage registration) =>
        registration with { Type = CoapMessageType.NonConfirmable, MessageId = TakeMessageId() };

    private ushort TakeMessageId() => unchecked(nextMessageId++);

    /// <summary>Gives the Observe value a message carries, or <see langword="null" /> without a valid one.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The sequence number.</returns>
    internal static uint? SequenceOf(CoapMessage message) =>
        message.Options.FirstOrDefault(option => option.Number == CoapOptionNumber.Observe) is { Value.Length: <= MaximumObserveLength } observe
            ? CoapObserveValue.Decode(observe.Value)
            : null;

    /// <summary>Gives the GET that cancels <paramref name="registration" />: the same token and options, Observe 1, non-confirmable.</summary>
    /// <param name="registration">The registration request.</param>
    /// <returns>The deregistration.</returns>
    internal static CoapMessage DeregistrationOf(CoapMessage registration) => registration with
    {
        Type = CoapMessageType.NonConfirmable,
        MessageId = unchecked((ushort)(registration.MessageId + 1)),
        Options = [.. registration.Options.Where(option => option.Number != CoapOptionNumber.Observe), ObserveOptionOf(CoapObserveValue.Deregister)],
    };

    private static CoapOption ObserveOptionOf(uint value) => new(CoapOptionNumber.Observe, CoapObserveValue.Encode(value));

    /// <summary>Answers and takes one received datagram; gives <see langword="true" /> when it ended the observation.</summary>
    private async ValueTask<bool> AnswerAsync(ITransferContext context, CoapMessage registration, byte[] datagram)
    {
        CoapMessage? message = Decoded(datagram);
        if (message is null)
        {
            return false;
        }

        if (!IsOfObservation(registration, message))
        {
            await RejectAsync(message, context.CancellationToken).ConfigureAwait(false);
            return false;
        }

        await AcknowledgeAsync(message, context.CancellationToken).ConfigureAwait(false);
        return await TakeWhenNewerAsync(context, registration, message).ConfigureAwait(false);
    }

    private async ValueTask<bool> TakeWhenNewerAsync(ITransferContext context, CoapMessage registration, CoapMessage message)
    {
        if (!IsNotification(message))
        {
            await WriteAsync(context, message).ConfigureAwait(false);
            return true;
        }

        uint sequence = SequenceOf(message)!.Value;
        if (CoapNotificationOrder.IsNewer(lastSequence, lastArrival, sequence, timeProvider.GetUtcNow()))
        {
            await TakeAsync(context, registration, message, sequence).ConfigureAwait(false);
        }

        return false;
    }

    private async ValueTask TakeAsync(ITransferContext context, CoapMessage registration, CoapMessage notification, uint sequence)
    {
        lastSequence = sequence;
        lastArrival = timeProvider.GetUtcNow();
        lastMaxAge = MaxAgeOf(notification);
        await WriteAsync(context, await WholeOf(registration, notification, context.CancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives <paramref name="notification" /> with every block of its representation when it carries Block2 with M set,
    /// fetching the later blocks by GETs without Observe under the block fetch token (RFC 7959 section 2.6), else as it is.
    /// The block-wise transfer's first exchange is answered with the notification itself, so only the later blocks go out.
    /// </summary>
    private ValueTask<CoapMessage> WholeOf(CoapMessage registration, CoapMessage notification, CancellationToken stop)
    {
        var exchange = new CoapExchange(channel, timeProvider);
        CoapMessage? unanswered = notification;
        var blockFetch = new CoapBlockwiseTransfer((request, cancellation) =>
        {
            if (unanswered is { } first)
            {
                unanswered = null;
                return ValueTask.FromResult(first);
            }

            nextMessageId = unchecked((ushort)(request.MessageId + 1));
            return exchange.RunAsync(request, BlockFetchTimeout, cancellation);
        });
        CoapMessage request = registration with
        {
            MessageId = unchecked((ushort)(nextMessageId - 1)),
            Token = BlockFetchTokenOf(registration.Token),
            Options = [.. registration.Options.Where(option => option.Number != CoapOptionNumber.Observe)],
            Payload = [],
        };
        return blockFetch.RunAsync(request, stop);
    }

    private async ValueTask WriteAsync(ITransferContext context, CoapMessage notification)
    {
        context.Events.ReportInfo($"CoAP notification {notification.Code}");
        if (context.HeaderOutput is { } headerOutput)
        {
            await headerOutput.WriteAsync(Encoding.ASCII.GetBytes($"CoAP/1.0 {notification.Code}\r\n\r\n"), context.CancellationToken).ConfigureAwait(false);
        }

        await context.Output.WriteAsync(notification.Payload, context.CancellationToken).ConfigureAwait(false);
        await context.Output.WriteAsync(NotificationEnd, context.CancellationToken).ConfigureAwait(false);
        await context.Output.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        written += notification.Payload.Length + NotificationEnd.Length;
    }

    private static bool IsOfObservation(CoapMessage registration, CoapMessage message) =>
        message.Type is CoapMessageType.Confirmable or CoapMessageType.NonConfirmable
            && message.Code.Class >= 2 && message.Token.AsSpan().SequenceEqual(registration.Token);

    private ValueTask AcknowledgeAsync(CoapMessage notification, CancellationToken stop) =>
        notification.Type == CoapMessageType.Confirmable
            ? SendAsync(EmptyOf(CoapMessageType.Acknowledgement, notification), stop)
            : ValueTask.CompletedTask;

    private ValueTask RejectAsync(CoapMessage message, CancellationToken stop) =>
        message.Type is CoapMessageType.Confirmable or CoapMessageType.NonConfirmable
            ? SendAsync(EmptyOf(CoapMessageType.Reset, message), stop)
            : ValueTask.CompletedTask;

    private static CoapMessage EmptyOf(CoapMessageType type, CoapMessage answered) =>
        new(type, CoapCode.Empty, answered.MessageId, [], [], []);

    private static CoapMessage? Decoded(ReadOnlySpan<byte> datagram)
    {
        try
        {
            return CoapMessageEncoding.Decode(datagram);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private ValueTask SendAsync(CoapMessage message, CancellationToken stop) =>
        channel.SendAsync(CoapMessageEncoding.Encode(message), channel.ServerEndPoint, stop);
}
