using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Http;

/// <summary>
/// POSTs a form to a token or device authorization endpoint over a connection the <see cref="IConnector"/> opens, with TLS
/// for <c>https</c>, and reads the JSON response whole, whatever its status, so a 400 or 401 error response (RFC 6749
/// §5.2) can be read (ADR-1030).
/// </summary>
/// <remarks>
/// One request per connection, sent with <c>Connection: close</c>; the connection is never marked reusable. Every failure
/// is a failed <see cref="OAuth2HttpResult"/>, never an exception, except cancellation.
/// </remarks>
/// <param name="connector">Opens the connection to the endpoint.</param>
public sealed class OAuth2HttpClient(IConnector connector)
{
    /// <summary>Sends <paramref name="request"/> to <paramref name="endpoint"/> and reads the response.</summary>
    /// <param name="endpoint">The endpoint, an absolute <c>http</c> or <c>https</c> URL.</param>
    /// <param name="request">The form body and the <c>Authorization</c> value.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, or why none was read.</returns>
    public async Task<OAuth2HttpResult> PostFormAsync(Uri endpoint, OAuth2FormRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(request);
        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            return OAuth2HttpResult.Failed(
                OAuth2HttpFailure.UnsupportedEndpoint, CurlExitCode.UnsupportedProtocol, $"The endpoint \"{endpoint}\" is not an http or https URL.");
        }

        try
        {
            return OAuth2HttpResult.Received(await SendAsync(endpoint, request, cancellationToken));
        }
        catch (OAuth2HttpException failure)
        {
            return OAuth2HttpResult.Failed(failure.Failure, failure.ExitCode, failure.Message);
        }
        catch (IOException failure)
        {
            return OAuth2HttpResult.Failed(OAuth2HttpFailure.ConnectionFailed, CurlExitCode.RecvError, failure.Message);
        }
    }

    private async Task<OAuth2HttpResponse> SendAsync(Uri endpoint, OAuth2FormRequest request, CancellationToken cancellationToken)
    {
        var requestBytes = OAuth2HttpRequestWriter.Write(endpoint, request);
        var connect = await connector.ConnectAsync(new ConnectTarget(endpoint.IdnHost, endpoint.Port, endpoint.Scheme == Uri.UriSchemeHttps), cancellationToken);
        await using var connection = connect.Connection
            ?? throw new OAuth2HttpException(OAuth2HttpFailure.ConnectFailed, connect.ExitCode, $"Could not connect to {endpoint.IdnHost}: {connect.ErrorMessage}");
        return await OAuth2HttpExchange.ExchangeAsync(connection, requestBytes, cancellationToken);
    }
}
