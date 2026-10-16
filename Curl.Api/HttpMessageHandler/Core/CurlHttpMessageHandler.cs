using Curl.Api.Errors;

namespace Curl.Api;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that sends each <see cref="HttpRequestMessage"/> through a
/// <see cref="CurlClient"/> (BL-2510, ADR-0647), so <c>new HttpClient(new CurlHttpMessageHandler(client))</c>
/// transfers with Curl's engine: the method, URI, version, request and content headers and body go into a
/// <see cref="CurlRequest"/>, and the <see cref="CurlResponse"/> comes back as an
/// <see cref="HttpResponseMessage"/> whose content streams the body and whose trailing headers fill when
/// the body has been read to its end. The handler does not own the client; dispose the client yourself.
/// </summary>
public sealed class CurlHttpMessageHandler : HttpMessageHandler
{
    /// <summary>A handler that sends every request through <paramref name="client"/>.</summary>
    /// <param name="client">The client the requests are sent through; not disposed with the handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    public CurlHttpMessageHandler(CurlClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        Client = client;
    }

    /// <summary>The client every request is sent through.</summary>
    public CurlClient Client { get; }

    /// <summary>
    /// Sends <paramref name="request"/> through <see cref="Client"/> and returns its response as soon as
    /// the body starts or the transfer ends. A cancelled <paramref name="cancellationToken"/> (as
    /// <see cref="HttpClient.Timeout"/> cancels it) aborts the transfer, while the body streams too.
    /// </summary>
    /// <param name="request">The request to send; its <see cref="HttpRequestMessage.RequestUri"/> must be absolute.</param>
    /// <param name="cancellationToken">Aborts the transfer.</param>
    /// <returns>The response, with <see cref="HttpResponseMessage.RequestMessage"/> set to <paramref name="request"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The request has no absolute URI.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="HttpRequestException">The transfer failed; its inner exception is the <see cref="CurlException"/>.</exception>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CurlRequest curlRequest = await HttpRequestMapping.ToCurlRequestAsync(request, Client.Options.Http, cancellationToken).ConfigureAwait(false);
        CurlResponse response;
        try
        {
            response = await Client.SendAsync(curlRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (CurlException exception)
        {
            throw new HttpRequestException(exception.Message, exception);
        }

        return HttpResponseMapping.ToHttpResponseMessage(response, request);
    }
}
