using System.Net.Sockets;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Client;

/// <summary>
/// Sends one confirmable request over a datagram channel and waits for its response (RFC 7252
/// sections 4.2, 4.8, 5.2 and 5.3): retransmits it at the initial timeout, doubled each time, up to
/// MAX_RETRANSMIT (4) times; takes a piggybacked response from the ACK carrying its Message ID; after an
/// empty ACK stops retransmitting and waits up to EXCHANGE_LIFETIME (247 seconds) for a separate
/// response carrying its token, acknowledging it when it is confirmable; rejects a response carrying an unrecognised critical option (RFC 7252 section 5.4.1) as a message that is not its response; fails on a Reset; resets any
/// other confirmable message, and ignores the rest.
/// </summary>
/// <param name="channel">The channel to the server.</param>
/// <param name="timeProvider">The clock the timeouts run on.</param>
internal sealed class CoapExchange(IDatagramChannel channel, TimeProvider timeProvider)
{
    /// <summary>MAX_RETRANSMIT (RFC 7252 section 4.8).</summary>
    public const int MaximumRetransmissions = 4;

    /// <summary>EXCHANGE_LIFETIME (RFC 7252 section 4.8.2), how long a separate response may take.</summary>
    public static readonly TimeSpan ExchangeLifetime = TimeSpan.FromSeconds(247);

    private const int MaximumDatagramLength = 65535;

    /// <summary>The registered option numbers this client recognises (<see cref="CoapOptionNumber" />).</summary>
    private static readonly HashSet<int> RecognisedOptionNumbers =
    [
        CoapOptionNumber.IfMatch, CoapOptionNumber.UriHost, CoapOptionNumber.ETag, CoapOptionNumber.IfNoneMatch,
        CoapOptionNumber.Observe, CoapOptionNumber.UriPort, CoapOptionNumber.LocationPath, CoapOptionNumber.UriPath,
        CoapOptionNumber.ContentFormat, CoapOptionNumber.MaxAge, CoapOptionNumber.UriQuery, CoapOptionNumber.Accept,
        CoapOptionNumber.LocationQuery, CoapOptionNumber.Block2, CoapOptionNumber.Block1, CoapOptionNumber.Size2,
        CoapOptionNumber.ProxyUri, CoapOptionNumber.ProxyScheme, CoapOptionNumber.Size1,
    ];

    private enum Reply
    {
        Ignored,
        Response,
        EmptyAcknowledgement,
        Reset,
    }

    /// <summary>Sends <paramref name="request" /> and gives its response.</summary>
    /// <param name="request">The confirmable request.</param>
    /// <param name="initialTimeout">The first retransmission timeout, ACK_TIMEOUT times a random factor between 1 and ACK_RANDOM_FACTOR.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response.</returns>
    /// <exception cref="CoapException">The server reset the request (exit 56), the socket failed (exit 55 sending, 56 receiving) or the server did not answer in time (exit 28).</exception>
    public async ValueTask<CoapMessage> RunAsync(CoapMessage request, TimeSpan initialTimeout, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            return await AwaitResponseAsync(request, initialTimeout, stop.Token).ConfigureAwait(false);
        }
        finally
        {
            stop.Cancel();
        }
    }

    private async ValueTask<CoapMessage> AwaitResponseAsync(CoapMessage request, TimeSpan timeout, CancellationToken stop)
    {
        byte[] encoded = CoapMessageEncoding.Encode(request);
        byte[] buffer = new byte[MaximumDatagramLength];
        int retransmissions = 0;
        bool acknowledged = false;
        await SendAsync(encoded, stop).ConfigureAwait(false);
        Task delay = Task.Delay(timeout, timeProvider, stop);
        Task<DatagramReceived> receive = channel.ReceiveAsync(buffer, stop).AsTask();
        while (true)
        {
            if (await Task.WhenAny(receive, delay).ConfigureAwait(false) != receive)
            {
                stop.ThrowIfCancellationRequested();
                ThrowWhenOutOfTime(acknowledged, retransmissions);
                retransmissions++;
                timeout *= 2;
                await SendAsync(encoded, stop).ConfigureAwait(false);
                delay = Task.Delay(timeout, timeProvider, stop);
                continue;
            }

            CoapMessage? message = Decoded(buffer.AsSpan(0, (await ReceivedAsync(receive).ConfigureAwait(false)).Length));
            Reply reply = Classify(request, message);
            await AnswerAsync(message, reply, stop).ConfigureAwait(false);
            switch (reply)
            {
                case Reply.Response:
                    return message!;
                case Reply.EmptyAcknowledgement when !acknowledged:
                    acknowledged = true;
                    delay = Task.Delay(ExchangeLifetime, timeProvider, stop);
                    break;
                case Reply.Reset:
                    throw new CoapException(CurlExitCode.RecvError, "CoAP server reset the request");
            }

            // Receives the next datagram only when the exchange goes on, so none after the response is taken here (RFC 7641 notifications).
            receive = channel.ReceiveAsync(buffer, stop).AsTask();
        }
    }

    private static void ThrowWhenOutOfTime(bool acknowledged, int retransmissions)
    {
        if (acknowledged)
        {
            throw new CoapException(CurlExitCode.OperationTimedOut, "CoAP separate response did not arrive within 247 seconds of its acknowledgement");
        }

        if (retransmissions == MaximumRetransmissions)
        {
            throw new CoapException(CurlExitCode.OperationTimedOut, "CoAP server did not answer after 4 retransmissions");
        }
    }

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

    private static Reply Classify(CoapMessage request, CoapMessage? message)
    {
        if (message is null)
        {
            return Reply.Ignored;
        }

        Reply reply = IsOwnReply(request, message) ? ClassifyOwnReply(request, message) : SeparateReply(request, message);
        return reply == Reply.Response && CarriesUnrecognisedCriticalOption(message) ? Reply.Ignored : reply;
    }

    private static Reply SeparateReply(CoapMessage request, CoapMessage message) =>
        IsSeparateResponse(request, message) ? Reply.Response : Reply.Ignored;

    /// <summary>
    /// Whether <paramref name="message" /> carries an option of class critical (an odd number) that is not
    /// a registered option this client knows; such a response is rejected (RFC 7252 section 5.4.1).
    /// </summary>
    private static bool CarriesUnrecognisedCriticalOption(CoapMessage message) =>
        message.Options.Any(option => option.Number % 2 == 1 && !RecognisedOptionNumbers.Contains(option.Number));

    private static bool IsOwnReply(CoapMessage request, CoapMessage message) =>
        message.MessageId == request.MessageId && message.Type is CoapMessageType.Acknowledgement or CoapMessageType.Reset;

    private static bool IsSeparateResponse(CoapMessage request, CoapMessage message) =>
        message.Type is CoapMessageType.Confirmable or CoapMessageType.NonConfirmable
            && message.Code.Class >= 2 && message.Token.AsSpan().SequenceEqual(request.Token);

    /// <summary>Acknowledges a confirmable response and resets any other confirmable message (RFC 7252 section 4.2).</summary>
    private ValueTask AnswerAsync(CoapMessage? message, Reply reply, CancellationToken stop) =>
        message is { Type: CoapMessageType.Confirmable }
            ? SendAsync(CoapMessageEncoding.Encode(new CoapMessage(reply == Reply.Response ? CoapMessageType.Acknowledgement : CoapMessageType.Reset, CoapCode.Empty, message.MessageId, [], [], [])), stop)
            : ValueTask.CompletedTask;

    private static Reply ClassifyOwnReply(CoapMessage request, CoapMessage message)
    {
        if (message.Type == CoapMessageType.Reset)
        {
            return Reply.Reset;
        }

        if (message.Code == CoapCode.Empty)
        {
            return Reply.EmptyAcknowledgement;
        }

        return message.Token.AsSpan().SequenceEqual(request.Token) ? Reply.Response : Reply.Ignored;
    }

    /// <summary>Gives the datagram a receive got; a socket error (a refusal after a datagram was received, say) ends with exit 56.</summary>
    private static async ValueTask<DatagramReceived> ReceivedAsync(Task<DatagramReceived> receive)
    {
        try
        {
            return await receive.ConfigureAwait(false);
        }
        catch (SocketException exception) when (exception.GetType() == typeof(SocketException))
        {
            throw new CoapException(CurlExitCode.RecvError, $"CoAP receive failed: {exception.Message}");
        }
    }

    /// <summary>Sends a datagram to the server; a socket error ends with exit 55.</summary>
    private async ValueTask SendAsync(byte[] datagram, CancellationToken stop)
    {
        try
        {
            await channel.SendAsync(datagram, channel.ServerEndPoint, stop).ConfigureAwait(false);
        }
        catch (SocketException exception) when (exception.GetType() == typeof(SocketException))
        {
            throw new CoapException(CurlExitCode.SendError, $"CoAP send failed: {exception.Message}");
        }
    }
}
