using Curl.Extended.Protocol.SignalR.Negotiate;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>
/// Runs the negotiate exchange (TransportProtocols.md, "POST [endpoint-base]/negotiate request"): <c>POST</c> to the
/// negotiate URL with an empty body, follow one redirect to its <c>url</c> with its <c>accessToken</c> as a bearer
/// token, and give the WebSocket URL of the connection the server opened.
/// </summary>
/// <param name="openConnection">Opens a connection to the host and port of a URL; each exchange has its own, disposed after it.</param>
/// <param name="requestFields">The fields curl's options add to each request, or <see langword="null"/> for none.</param>
/// <param name="sendsCredentialsToOtherOrigins"><c>--location-trusted</c>: keep the user's <c>Authorization</c> and <c>Cookie</c> on a redirect to another scheme, host or port.</param>
internal sealed class SignalRNegotiator(
    Func<Uri, CancellationToken, ValueTask<IConnection>> openConnection,
    IReadOnlyList<KeyValuePair<string, string>>? requestFields = null,
    bool sendsCredentialsToOtherOrigins = false)
{
    /// <summary>Negotiates with the endpoint at <paramref name="endpointUrl"/>.</summary>
    /// <exception cref="SignalRTransportException">
    /// The status is not 2xx, the server answered with an error, redirected twice, gave a redirect URL that is not
    /// absolute, or offers no WebSockets transport.
    /// </exception>
    /// <exception cref="SignalRWireFormatException">The body is not a negotiate response.</exception>
    public async Task<NegotiatedWebSocketEndpoint> NegotiateAsync(Uri endpointUrl, CancellationToken cancellationToken)
    {
        NegotiateResponse response = await ExchangeAsync(endpointUrl, null, cancellationToken);
        if (!response.IsRedirect)
        {
            return WebSocketEndpoint(endpointUrl, response, null, requestFields);
        }

        Uri redirectUrl = TryReadHttpUrl(response.Url) ?? throw new SignalRTransportException($"The negotiate response redirected to a URL that is not absolute: {response.Url}");

        // Like -L without --location-trusted: another scheme, host or port gets none of the user's Authorization or Cookie (BL-5311, BL-5377).
        IReadOnlyList<KeyValuePair<string, string>>? redirectFields = FieldsForRedirect(endpointUrl, redirectUrl);
        NegotiateResponse redirected = await ExchangeAsync(redirectUrl, response.AccessToken, cancellationToken, redirectFields);
        return redirected.IsRedirect
            ? throw new SignalRTransportException("The negotiate response redirected a second time.")
            : WebSocketEndpoint(redirectUrl, redirected, response.AccessToken, redirectFields);
    }

    private IReadOnlyList<KeyValuePair<string, string>>? FieldsForRedirect(Uri endpointUrl, Uri redirectUrl) =>
        sendsCredentialsToOtherOrigins || IsSameOrigin(endpointUrl, redirectUrl)
            ? requestFields
            : (requestFields ?? []).Where(field => !IsUserCredential(field.Key)).ToList();

    private static bool IsUserCredential(string name) =>
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrigin(Uri first, Uri second) =>
        first.Scheme == second.Scheme && string.Equals(first.IdnHost, second.IdnHost, StringComparison.OrdinalIgnoreCase) && first.Port == second.Port;

    // On Linux and macOS Uri reads a rooted path such as "/elsewhere" as an absolute file URL, so the scheme decides.
    private static Uri? TryReadHttpUrl(string? text) =>
        Uri.TryCreate(text, UriKind.Absolute, out Uri? url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;

    private static NegotiatedWebSocketEndpoint WebSocketEndpoint(Uri endpointUrl, NegotiateResponse response, string? accessToken, IReadOnlyList<KeyValuePair<string, string>>? fields) =>
        response.OffersWebSockets
            ? new NegotiatedWebSocketEndpoint(SignalRUrls.WebSocketUrl(endpointUrl, response), accessToken, fields)
            : throw new SignalRTransportException("The negotiate response offers no WebSockets transport.");

    // A refused login (401, 403) exits 22 like the port's other WebSocket schemes; any other non-2xx status exits 8.
    private static void ThrowUnlessSuccessful(int statusCode)
    {
        if (statusCode is >= 200 and <= 299)
        {
            return;
        }

        throw new SignalRTransportException(
            $"The negotiate request failed with HTTP status {statusCode}.",
            statusCode is 401 or 403 ? CurlExitCode.HttpReturnedError : CurlExitCode.WeirdServerReply);
    }

    private async Task<NegotiateResponse> ExchangeAsync(Uri endpointUrl, string? accessToken, CancellationToken cancellationToken, IReadOnlyList<KeyValuePair<string, string>>? fields = null)
    {
        fields ??= requestFields;
        Uri negotiateUrl = SignalRUrls.NegotiateUrl(endpointUrl);
        await using IConnection connection = await openConnection(negotiateUrl, cancellationToken);
        byte[] request = HttpRequestWriter.Write(
            "POST",
            negotiateUrl,
            HttpRequestWriter.Combine([new("Content-Length", "0")], fields, accessToken));
        await connection.WriteAsync(request, cancellationToken);
        await connection.FlushAsync(cancellationToken);

        BufferedConnectionReader reader = new(connection);
        HttpResponseHead head = await HttpResponseReader.ReadHeadAsync(reader, cancellationToken);
        byte[] body = await HttpResponseReader.ReadBodyAsync(reader, head, cancellationToken);
        ThrowUnlessSuccessful(head.StatusCode);

        NegotiateResponse response = NegotiateResponseReader.Read(body);
        return response.Error is null
            ? response
            : throw new SignalRTransportException($"The server refused the negotiate request: {response.Error}");
    }
}
