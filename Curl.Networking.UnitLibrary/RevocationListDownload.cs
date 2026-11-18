using System.Diagnostics.CodeAnalysis;

namespace Curl.Networking;

/// <summary>
/// Downloads a certificate revocation list over plain HTTP from a CRL distribution point.
/// Thin on purpose, like the transport types: it needs a live server, so it is excluded from
/// coverage and the tests replace <see cref="ServerCertificateVerification.FetchRevocationList" />.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin HTTP adapter that needs a live server; tests replace ServerCertificateVerification.FetchRevocationList.")]
internal static class RevocationListDownload
{
    /// <summary>The most a list's body may hold before it counts as unfetchable.</summary>
    internal const int MaximumBodyBytes = 10 * 1024 * 1024;

    /// <summary>Gets the list at <paramref name="uri" />; throws when it cannot be fetched, saying why.</summary>
    /// <param name="uri">The distribution point's http address.</param>
    /// <param name="cancellation">Cuts the fetch short, headers and body both.</param>
    /// <returns>The response body.</returns>
    /// <exception cref="IOException">The server did not answer with success, or the body passed <see cref="MaximumBodyBytes" />.</exception>
    internal static byte[] Fetch(Uri uri, CancellationToken cancellation)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            throw new IOException($"the server answered {(int)response.StatusCode}");
        }

        using var body = response.Content.ReadAsStream(cancellation);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = body.Read(chunk, 0, chunk.Length)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            if (buffer.Length + read > MaximumBodyBytes)
            {
                throw new IOException($"the list is larger than {MaximumBodyBytes} bytes");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
