using System.Text;
using Curl.Extension.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Client;

/// <summary>
/// Serves <c>coap://host[:port]/path[?query]</c> (ADR-1045): sends one confirmable request over UDP,
/// GET, or POST, PUT or DELETE as <c>-X</c> and <c>-d</c> choose, with the <c>-d</c> data as its payload,
/// and writes the response payload. A 4.xx or 5.xx response ends with exit 0 as an HTTP error status
/// does, or with exit 22 under <c>-f</c> or <c>--fail-with-body</c>. <c>-i</c> writes the response code
/// as a header line before the payload, and <c>-v</c> reports it.
/// </summary>
/// <param name="connector">Opens the datagram channel to the server.</param>
/// <param name="randomBytes">Gives the given number of random bytes, for the Message ID, the token and the timeout's random factor.</param>
internal sealed class CoapProtocolHandler(IDatagramConnector connector, Func<int, byte[]> randomBytes) : IProtocolHandler
{
    /// <summary>ACK_TIMEOUT (RFC 7252 section 4.8).</summary>
    public static readonly TimeSpan AcknowledgementTimeout = TimeSpan.FromSeconds(2);

    private const int TokenLength = 4;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["coap"];

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
        CoapMessage request = RequestOf(context);
        DatagramOpenResult open = await connector.OpenAsync(context.Url.IdnHost, CoapRequest.PortOf(context.Url), context.CancellationToken).ConfigureAwait(false);
        if (open.Channel is not { } channel)
        {
            return new TransferResult(open.ExitCode, 0, open.ErrorMessage);
        }

        await using (channel.ConfigureAwait(false))
        {
            context.Progress.ReportTransferStarted();
            CoapMessage response = await new CoapExchange(channel, context.TimeProvider)
                .RunAsync(request, InitialTimeoutOf(randomBytes(1)[0]), context.CancellationToken).ConfigureAwait(false);
            return await WriteResponseAsync(context, response).ConfigureAwait(false);
        }
    }

    private CoapMessage RequestOf(ITransferContext context)
    {
        CoapCode method = CoapRequest.MethodOf(context);
        byte[] identifier = randomBytes(2);
        return new CoapMessage(
            CoapMessageType.Confirmable,
            method,
            (ushort)((identifier[0] << 8) | identifier[1]),
            randomBytes(TokenLength),
            CoapRequest.UriOptionsOf(context.Url),
            context.PostData?.ToArray() ?? []);
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
