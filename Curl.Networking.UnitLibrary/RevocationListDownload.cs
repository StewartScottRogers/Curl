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
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    /// <summary>Gets the list at <paramref name="uri" />, or <see langword="null" /> when it cannot be fetched.</summary>
    /// <param name="uri">The distribution point's http address.</param>
    /// <returns>The response body, or <see langword="null" />.</returns>
    internal static byte[]? Fetch(Uri uri)
    {
        try
        {
            using var client = new HttpClient { Timeout = Limit };
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = client.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var body = response.Content.ReadAsStream();
            using var buffer = new MemoryStream();
            body.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
