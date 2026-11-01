using System.Text;
using Curl.Extended.Protocol.Coap.Blockwise;
using Curl.Extended.Protocol.Coap.Observe;
using Curl.Extended.Protocol.Coap.Secure;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Client;

/// <summary>
/// Serves <c>coap://host[:port]/path[?query]</c> (ADR-1047): sends one confirmable request over UDP,
/// GET, or POST, PUT or DELETE as <c>-X</c>, <c>-d</c> and <c>-T</c> choose, with the <c>-d</c> data or the <c>-T</c>
/// file as its payload, block-wise when either needs more than one block (RFC 7959), and writes the whole response payload. A 4.xx or 5.xx response ends with exit 0 as an HTTP error status
/// does, or with exit 22 under <c>-f</c> or <c>--fail-with-body</c>. <c>-i</c> writes the response code
/// as a header line before the payload, and <c>-v</c> reports it. Under <c>--coap-observe</c> the request
/// registers an observation and the handler writes each notification (RFC 7641, ADR-1064).
/// <c>coaps://</c> runs the same requests over DTLS 1.2 to port 5684 (RFC 7252 section 9.1, ADR-1042).
/// </summary>
/// <param name="connector">Opens the datagram channel to the server.</param>
/// <param name="randomBytes">Gives the given number of random bytes, for the Message ID, the token and the timeout's random factor.</param>
/// <param name="tlsProvider">Verifies a <c>coaps://</c> server's certificate chain under curl's trust settings.</param>
internal sealed class CoapProtocolHandler(IDatagramConnector connector, Func<int, byte[]> randomBytes, ITlsProvider tlsProvider) : IProtocolHandler
{
    /// <summary>ACK_TIMEOUT (RFC 7252 section 4.8).</summary>
    public static readonly TimeSpan AcknowledgementTimeout = TimeSpan.FromSeconds(2);

    private const int TokenLength = 4;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["coap", CoapRequest.SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context).ConfigureAwait(false);
        }
        catch (CoapException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>
    /// Gives the first retransmission timeout: ACK_TIMEOUT times a factor from 1 to ACK_RANDOM_FACTOR
    /// (1.5) that a random byte picks, so 2 to 3 seconds (RFC 7252 section 4.2).
    /// </summary>
    /// <param name="random">The random byte.</param>
    /// <returns>The timeout.</returns>
    internal static TimeSpan InitialTimeoutOf(byte random) =>
        AcknowledgementTimeout + (AcknowledgementTimeout / 2 * random / byte.MaxValue);

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        CoapMessage request = await RequestOfAsync(context).ConfigureAwait(false);
        DatagramOpenResult open = await connector.OpenAsync(context.Url.IdnHost, CoapRequest.PortOf(context.Url), context.CancellationToken).ConfigureAwait(false);
        if (open.Channel is not { } transport)
        {
            return new TransferResult(open.ExitCode, 0, open.ErrorMessage);
        }

        await using (transport.ConfigureAwait(false))
        {
            return CoapRequest.IsSecure(context.Url)
                ? await SecureExchangeAsync(context, transport, request).ConfigureAwait(false)
                : await ExchangeAsync(context, transport, request).ConfigureAwait(false);
        }
    }

    /// <summary>Runs the DTLS 1.2 handshake over <paramref name="transport" /> and the exchange over the secured channel (RFC 7252 section 9.1).</summary>
    private async ValueTask<TransferResult> SecureExchangeAsync(ITransferContext context, IDatagramChannel transport, CoapMessage request)
    {
        DtlsDatagramChannel secured = await DtlsDatagramChannel.OpenAsync(transport, tlsProvider, context.Url.IdnHost, context.TimeProvider, context.CancellationToken).ConfigureAwait(false);
        await using (secured.ConfigureAwait(false))
        {
            return await ExchangeAsync(context, secured, request).ConfigureAwait(false);
        }
    }

    /// <summary>Sends <paramref name="request" /> over <paramref name="channel" />, block-wise or as an observation, and writes the response.</summary>
    private async ValueTask<TransferResult> ExchangeAsync(ITransferContext context, IDatagramChannel channel, CoapMessage request)
    {
        context.Progress.ReportTransferStarted();
        var exchange = new CoapExchange(channel, context.TimeProvider);
        TimeSpan initialTimeout = InitialTimeoutOf(randomBytes(1)[0]);
        if (context is CoapObserveContext)
        {
            CoapMessage registration = CoapObservation.RegistrationOf(request);
            return await ObserveAsync(context, channel, registration, exchange.RunAsync(registration, initialTimeout, context.CancellationToken)).ConfigureAwait(false);
        }

        CoapMessage response = await new CoapBlockwiseTransfer((message, stop) => exchange.RunAsync(message, initialTimeout, stop))
            .RunAsync(request, context.CancellationToken).ConfigureAwait(false);
        return await WriteResponseAsync(context, response).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers an observation (RFC 7641 section 3.1, ADR-1064) and follows its notifications, or writes the
    /// response as a plain GET's when the server answers without an Observe option.
    /// </summary>
    private static async ValueTask<TransferResult> ObserveAsync(ITransferContext context, IDatagramChannel channel, CoapMessage registration, ValueTask<CoapMessage> exchange)
    {
        CoapMessage response = await exchange.ConfigureAwait(false);
        return CoapObservation.IsNotification(response)
            ? await new CoapObservation(channel, context.TimeProvider).FollowAsync(context, registration, response).ConfigureAwait(false)
            : await WriteResponseAsync(context, response).ConfigureAwait(false);
    }

    private async ValueTask<CoapMessage> RequestOfAsync(ITransferContext context)
    {
        CoapCode method = CoapRequest.MethodOf(context);
        byte[] identifier = randomBytes(2);
        return new CoapMessage(
            CoapMessageType.Confirmable,
            method,
            (ushort)((identifier[0] << 8) | identifier[1]),
            randomBytes(TokenLength),
            CoapRequest.UriOptionsOf(context.Url),
            await CoapRequest.PayloadOfAsync(context).ConfigureAwait(false));
    }

    private static async ValueTask<TransferResult> WriteResponseAsync(ITransferContext context, CoapMessage response)
    {
        context.Events.ReportInfo($"CoAP response {response.Code}");
        HttpFailMode fail = response.Code.Class >= 4 ? context.Http?.Fail ?? HttpFailMode.None : HttpFailMode.None;
        if (fail == HttpFailMode.Fail)
        {
            return ErrorResponseResult(response);
        }

        await WriteResponseCodeAsync(context, response).ConfigureAwait(false);
        await context.Output.WriteAsync(response.Payload, context.CancellationToken).ConfigureAwait(false);
        return fail == HttpFailMode.FailWithBody ? ErrorResponseResult(response) : TransferResult.Success(response.Payload.Length);
    }

    private static ValueTask WriteResponseCodeAsync(ITransferContext context, CoapMessage response) =>
        context.HeaderOutput is { } headerOutput
            ? headerOutput.WriteAsync(Encoding.ASCII.GetBytes($"CoAP/1.0 {response.Code}\r\n\r\n"), context.CancellationToken)
            : ValueTask.CompletedTask;

    private static TransferResult ErrorResponseResult(CoapMessage response) =>
        TransferResult.Failure(CurlExitCode.HttpReturnedError, $"The requested URL returned error: {response.Code}");
}
