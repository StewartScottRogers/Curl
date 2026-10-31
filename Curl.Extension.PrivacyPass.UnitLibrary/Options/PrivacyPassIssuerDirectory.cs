using System.Text.Json;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Tokens;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Options;

/// <summary>
/// An issuer's directory (RFC 9578 §4), read from <c>/.well-known/private-token-issuer-directory</c> at the issuer's
/// origin: the request URL tokens are issued at and the issuer's Blind RSA token keys (token type <c>0x0002</c>).
/// </summary>
/// <param name="RequestUrl">The <c>issuer-request-uri</c>, resolved against the directory's URL.</param>
/// <param name="TokenKeys">The decoded <c>token-key</c> of every <c>token-keys</c> entry of token type <c>0x0002</c>.</param>
internal sealed record PrivacyPassIssuerDirectory(Uri RequestUrl, IReadOnlyList<byte[]> TokenKeys)
{
    /// <summary>The directory's path at the issuer's origin (RFC 9578 §4, §8.1).</summary>
    public const string WellKnownPath = "/.well-known/private-token-issuer-directory";

    /// <summary>The directory's media type (RFC 9578 §8.3).</summary>
    public const string MediaType = "application/private-token-issuer-directory";

    /// <summary>Tells whether <c>--privacy-pass</c>'s URL is an issuer origin, to discover from, rather than a request URL.</summary>
    /// <param name="url">The absolute URL.</param>
    /// <returns><see langword="true"/> when the URL has no path beyond <c>/</c> and no query.</returns>
    public static bool IsIssuerOrigin(Uri url) => url.AbsolutePath == "/" && url.Query.Length == 0;

    /// <summary>GETs and reads the directory at <paramref name="issuerOrigin"/>.</summary>
    /// <param name="client">Sends the request.</param>
    /// <param name="issuerOrigin">The issuer's origin.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The directory.</returns>
    /// <exception cref="PrivacyPassException">The directory could not be fetched or read.</exception>
    public static async Task<PrivacyPassIssuerDirectory> FetchAsync(PrivacyPassHttpClient client, Uri issuerOrigin, CancellationToken cancellationToken)
    {
        var directoryUrl = new Uri(issuerOrigin, WellKnownPath);
        var result = await client.SendAsync(new PrivacyPassHttpRequest("GET", directoryUrl, [new("Accept", MediaType)], null), cancellationToken);
        var response = result.Response ?? throw new PrivacyPassException(result.ExitCode, result.Message!);
        if (response.StatusCode / 100 != 2)
        {
            throw new PrivacyPassException(CurlExitCode.LoginDenied, $"The issuer directory {directoryUrl} answered {response.StatusCode}.");
        }

        return Parse(response.Body, directoryUrl)
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, $"The issuer directory {directoryUrl} is not a JSON object with an issuer-request-uri and token-keys.");
    }

    /// <summary>Reads a directory object (RFC 9578 §4).</summary>
    /// <param name="body">The JSON body.</param>
    /// <param name="directoryUrl">The URL it was read from, which a relative <c>issuer-request-uri</c> is resolved against.</param>
    /// <returns>The directory, or <see langword="null"/> when the body is not a directory object.</returns>
    public static PrivacyPassIssuerDirectory? Parse(byte[] body, Uri directoryUrl)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            List<byte[]> tokenKeys = [.. root.GetProperty("token-keys").EnumerateArray().Select(BlindRsaTokenKey).OfType<byte[]>()];
            return Uri.TryCreate(directoryUrl, root.GetProperty("issuer-request-uri").GetString(), out var requestUrl)
                ? new PrivacyPassIssuerDirectory(requestUrl, tokenKeys)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Tells whether the directory lists <paramref name="tokenKey"/> among its Blind RSA keys.</summary>
    /// <param name="tokenKey">A challenge's <c>token-key</c>.</param>
    /// <returns><see langword="true"/> when it is listed.</returns>
    public bool Lists(byte[] tokenKey) => TokenKeys.Any(key => key.AsSpan().SequenceEqual(tokenKey));

    // A token-keys entry: its token-key when its token-type is 2, otherwise null.
    private static byte[]? BlindRsaTokenKey(JsonElement entry) =>
        entry.GetProperty("token-type").GetInt32() == PrivacyPassIssuance.BlindRsaTokenType
            ? Base64Url.Decode(entry.GetProperty("token-key").GetString() ?? string.Empty)
            : null;
}
