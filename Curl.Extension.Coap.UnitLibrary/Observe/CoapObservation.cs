using System.Text;
using Curl.Extension.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Observe;

/// <summary>
/// Follows one observation once the server has answered its registration with a notification (RFC 7641,
/// ADR-1064): writes each notification as it arrives, its payload and then a newline, under <c>-i</c> after a
/// <c>CoAP/1.0 c.dd</c> line; drops one older than the last taken (section 3.4); acknowledges a confirmable one
/// (section 3.5); resets any other message (section 3.6); ends after a notification that is not a 2.xx or carries no
/// Observe option (section 3.2); and when the transfer is cancelled, at <c>--max-time</c> or by interruption,
/// deregisters with a GET carrying Observe 1 and the registration's token (section 3.6) before ending.
/// </summary>
/// <param name="channel">The channel the registration went over.</param>
/// <param name="timeProvider">The clock the arrival times are read from.</param>
internal sealed class CoapObservation(IDatagramChannel channel, TimeProvider timeProvider)
{
    private const int MaximumDatagramLength = 65535;

    private const int MaximumObserveLength = 3;

    private static readonly byte[] NotificationEnd = "\n"u8.ToArray();

    private uint lastSequence;

    private DateTimeOffset lastArrival;

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
        await TakeAsync(context, first, SequenceOf(first)!.Value).ConfigureAwait(false);
        OperationCanceledException cancellation;
        try
        {
            byte[] buffer = new byte[MaximumDatagramLength];
            while (!await ReceiveAsync(context, registration, buffer).ConfigureAwait(false))
            {
            }

            return TransferResult.Success(written);
        }
        catch (OperationCanceledException exception)
        {
            cancellation = exception;
        }

        // Deregisters outside the catch block, so the compiler adds no rethrow branch of its own (RFC 7641 section 3.6).
        await SendAsync(DeregistrationOf(registration), CancellationToken.None).ConfigureAwait(false);
        throw cancellation;
    }

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

    /// <summary>Receives and answers one datagram; gives <see langword="true" /> when it ended the observation.</summary>
    private async ValueTask<bool> ReceiveAsync(ITransferContext context, CoapMessage registration, byte[] buffer)
    {
        DatagramReceived received = await channel.ReceiveAsync(buffer, context.CancellationToken).ConfigureAwait(false);
        CoapMessage? message = Decoded(buffer.AsSpan(0, received.Length));
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
        return await TakeWhenNewerAsync(context, message).ConfigureAwait(false);
    }

    private async ValueTask<bool> TakeWhenNewerAsync(ITransferContext context, CoapMessage message)
    {
        if (!IsNotification(message))
        {
            await WriteAsync(context, message).ConfigureAwait(false);
            return true;
        }

        uint sequence = SequenceOf(message)!.Value;
        if (CoapNotificationOrder.IsNewer(lastSequence, lastArrival, sequence, timeProvider.GetUtcNow()))
        {
            await TakeAsync(context, message, sequence).ConfigureAwait(false);
        }

        return false;
    }

    private async ValueTask TakeAsync(ITransferContext context, CoapMessage notification, uint sequence)
    {
        lastSequence = sequence;
        lastArrival = timeProvider.GetUtcNow();
        await WriteAsync(context, notification).ConfigureAwait(false);
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
