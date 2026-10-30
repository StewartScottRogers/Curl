using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Http;

/// <summary>
/// Sends one <see cref="PrivacyPassHttpRequest"/> to an issuer over a connection the <see cref="IConnector"/>
/// opens, with TLS for <c>https</c>, and reads the response whole, whatever its status, so a 401's challenge can be read.
/// </summary>
/// <remarks>
/// One request per connection, sent with <c>Connection: close</c>. Every failure is a failed <see cref="PrivacyPassHttpResult"/>,
/// never an exception, except cancellation.
/// </remarks>
/// <param name="connector">Opens the connection to the URL's host.</param>
public sealed class PrivacyPassHttpClient(IConnector connector)
{
    /// <summary>Sends <paramref name="request"/> and reads the response.</summary>
    /// <param name="request">The request; its URL must be an absolute <c>http</c> or <c>https</c> URL.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, or why none was read.</returns>
    public async Task<PrivacyPassHttpResult> SendAsync(PrivacyPassHttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var url = request.Url;
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return PrivacyPassHttpResult.Failed(
                PrivacyPassHttpFailure.UnsupportedEndpoint, CurlExitCode.UnsupportedProtocol, $"The URL \"{url}\" is not an http or https URL.");
        }

        try
        {
            return PrivacyPassHttpResult.Received(await ConnectAndExchangeAsync(request, cancellationToken));
        }
        catch (PrivacyPassHttpException failure)
        {
            return PrivacyPassHttpResult.Failed(failure.Failure, failure.ExitCode, failure.Message);
        }
        catch (IOException failure)
        {
            return PrivacyPassHttpResult.Failed(PrivacyPassHttpFailure.ConnectionFailed, CurlExitCode.RecvError, failure.Message);
        }
    }

    private async Task<PrivacyPassHttpResponse> ConnectAndExchangeAsync(PrivacyPassHttpRequest request, CancellationToken cancellationToken)
    {
        var url = request.Url;
        var connect = await connector.ConnectAsync(new ConnectTarget(url.IdnHost, url.Port, url.Scheme == Uri.UriSchemeHttps), cancellationToken);
        await using var connection = connect.Connection
            ?? throw new PrivacyPassHttpException(PrivacyPassHttpFailure.ConnectFailed, connect.ExitCode, $"Could not connect to {url.IdnHost}: {connect.ErrorMessage}");
        return await PrivacyPassHttpExchange.ExchangeAsync(connection, request, cancellationToken);
    }
}
