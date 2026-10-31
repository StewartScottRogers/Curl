using Curl.Extension.Protocol.SignalR.Negotiate;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.SignalR.Transport;

/// <summary>
/// Runs the negotiate exchange (TransportProtocols.md, "POST [endpoint-base]/negotiate request"): <c>POST</c> to the
/// negotiate URL with an empty body, follow one redirect to its <c>url</c> with its <c>accessToken</c> as a bearer
/// token, and give the WebSocket URL of the connection the server opened.
/// </summary>
/// <param name="openConnection">Opens a connection to the host and port of a URL; each exchange has its own, disposed after it.</param>
internal sealed class SignalRNegotiator(Func<Uri, CancellationToken, ValueTask<IConnection>> openConnection)
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
            return WebSocketEndpoint(endpointUrl, response, null);
        }

        Uri redirectUrl = TryReadHttpUrl(response.Url) ?? throw new SignalRTransportException($"The negotiate response redirected to a URL that is not absolute: {response.Url}");
        NegotiateResponse redirected = await ExchangeAsync(redirectUrl, response.AccessToken, cancellationToken);
        return redirected.IsRedirect
            ? throw new SignalRTransportException("The negotiate response redirected a second time.")
            : WebSocketEndpoint(redirectUrl, redirected, response.AccessToken);
    }

    // On Linux and macOS Uri reads a rooted path such as "/elsewhere" as an absolute file URL, so the scheme decides.
    private static Uri? TryReadHttpUrl(string? text) =>
        Uri.TryCreate(text, UriKind.Absolute, out Uri? url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;

    private static NegotiatedWebSocketEndpoint WebSocketEndpoint(Uri endpointUrl, NegotiateResponse response, string? accessToken) =>
        response.OffersWebSockets
            ? new NegotiatedWebSocketEndpoint(SignalRUrls.WebSocketUrl(endpointUrl, response), accessToken)
            : throw new SignalRTransportException("The negotiate response offers no WebSockets transport.");

    private async Task<NegotiateResponse> ExchangeAsync(Uri endpointUrl, string? accessToken, CancellationToken cancellationToken)
    {
        Uri negotiateUrl = SignalRUrls.NegotiateUrl(endpointUrl);
        await using IConnection connection = await openConnection(negotiateUrl, cancellationToken);
        byte[] request = HttpRequestWriter.Write(
            "POST",
            negotiateUrl,
            [.. HttpRequestWriter.BearerAuthorization(accessToken), new("Content-Length", "0")]);
        await connection.WriteAsync(request, cancellationToken);
        await connection.FlushAsync(cancellationToken);

        BufferedConnectionReader reader = new(connection);
        HttpResponseHead head = await HttpResponseReader.ReadHeadAsync(reader, cancellationToken);
        byte[] body = await HttpResponseReader.ReadBodyAsync(reader, head, cancellationToken);
        if (head.StatusCode is < 200 or > 299)
        {
            throw new SignalRTransportException($"The negotiate request failed with HTTP status {head.StatusCode}.");
        }

        NegotiateResponse response = NegotiateResponseReader.Read(body);
        return response.Error is null
            ? response
            : throw new SignalRTransportException($"The server refused the negotiate request: {response.Error}");
    }
}
