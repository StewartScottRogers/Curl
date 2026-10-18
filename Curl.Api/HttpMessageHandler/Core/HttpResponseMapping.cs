using System.Net;

namespace Curl.Api;

/// <summary>Maps a <see cref="CurlResponse"/> onto the <see cref="HttpResponseMessage"/> <see cref="CurlHttpMessageHandler"/> returns (ADR-0647).</summary>
internal static class HttpResponseMapping
{
    /// <summary>
    /// The <see cref="HttpResponseMessage"/> for <paramref name="response"/>: its status, reason (none
    /// when empty, so the status's own phrase is given), version and headers, each header going to the
    /// message's headers or, when it is a content header, the content's; the content streams the body
    /// and fills <see cref="HttpResponseMessage.TrailingHeaders"/> once read to its end.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="request">The request it answers.</param>
    /// <returns>The response message.</returns>
    public static HttpResponseMessage ToHttpResponseMessage(CurlResponse response, HttpRequestMessage request)
    {
        HttpResponseMessage message = new((HttpStatusCode)response.StatusCode)
        {
            ReasonPhrase = response.ReasonPhrase.Length == 0 ? null : response.ReasonPhrase,
            Version = response.Version,
            RequestMessage = request,
        };
        message.Content = new System.Net.Http.StreamContent(new TrailerFillingStream(response.Body, response.Trailers, message.TrailingHeaders));
        foreach ((string name, string value) in response.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                _ = message.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return message;
    }
}
