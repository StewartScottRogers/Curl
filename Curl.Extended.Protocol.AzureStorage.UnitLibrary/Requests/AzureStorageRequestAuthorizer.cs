using System.Globalization;

namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>
/// Authorizes an Azure Storage request: a shared access signature is appended to the query ("Create a service SAS"),
/// an Entra ID token is sent as <c>Authorization: Bearer</c> ("Authorize with Microsoft Entra ID"), and every request
/// carries <c>x-ms-version</c> ("Versioning for the Azure Storage services") and <c>x-ms-date</c>.
/// </summary>
/// <param name="timeProvider">The clock <c>x-ms-date</c> is read from.</param>
public sealed class AzureStorageRequestAuthorizer(TimeProvider timeProvider)
{
    /// <summary>The service version every request names; bearer authorization needs 2017-11-09 or later.</summary>
    public const string ServiceVersion = "2025-01-05";

    /// <summary>Builds the request target and header fields for <paramref name="endpoint"/> under <paramref name="credential"/>.</summary>
    /// <param name="endpoint">Where the request goes.</param>
    /// <param name="credential">What authorizes it.</param>
    /// <returns>The request target and header fields.</returns>
    /// <param name="method">The request's verb, which a Shared Key signature covers.</param>
    /// <param name="extraHeaders">The header fields the caller sends after these, which a Shared Key signature covers; <see langword="null"/> for none.</param>
    /// <param name="bodyLength">The request body's length, which a Shared Key signature covers.</param>
    /// <exception cref="AzureStorageRequestException">The SAS token is not a query string, the bearer token cannot be a header value, or the account key is not base64.</exception>
    public AzureStorageAuthorizedRequest Authorize(AzureStorageEndpoint endpoint, AzureStorageCredential credential, string method = "GET", IReadOnlyList<KeyValuePair<string, string>>? extraHeaders = null, long bodyLength = 0)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        var headers = new List<KeyValuePair<string, string>>
        {
            new("Host", endpoint.HostHeader),
            new("x-ms-version", ServiceVersion),
            new("x-ms-date", timeProvider.GetUtcNow().ToString("R", CultureInfo.InvariantCulture)),
        };
        var query = endpoint.Query;
        if (credential.Kind == AzureStorageCredentialKind.SharedAccessSignature)
        {
            query = Joined(query, SasQuery(credential.Value));
        }
        else if (credential.Kind == AzureStorageCredentialKind.Bearer)
        {
            headers.Add(new("Authorization", "Bearer " + BearerToken(credential.Value)));
        }

        else if (credential.Kind == AzureStorageCredentialKind.SharedKey)
        {
            var stringToSign = AzureStorageSharedKeyStringToSign.Build(method, credential.Account, endpoint.Path, query, [.. headers, .. extraHeaders ?? []], bodyLength);
            headers.Add(new("Authorization", AzureStorageSharedKeySigner.AuthorizationOf(credential.Account, credential.Value, stringToSign)));
        }

        var target = query.Length == 0 ? endpoint.Path : endpoint.Path + "?" + query;
        return new AzureStorageAuthorizedRequest(target, headers);
    }

    /// <summary>Checks that a SAS token is a query string of <c>name=value</c> pairs carrying <c>sig</c>, and returns it without a leading <c>?</c>.</summary>
    /// <param name="token">The SAS token, with or without its leading <c>?</c>.</param>
    /// <returns>The token as a query without its <c>?</c>.</returns>
    /// <exception cref="AzureStorageRequestException">The token is not such a query string.</exception>
    public static string SasQuery(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var query = token.StartsWith('?') ? token[1..] : token;
        if (!IsSignedQuery(query))
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.InvalidSasToken, "The SAS token is not a query string of name=value pairs with a sig.");
        }

        return query;
    }

    private static string Joined(string query, string addition) => query.Length == 0 ? addition : query + "&" + addition;

    private static bool IsSignedQuery(string query)
    {
        var parameters = query.Split('&');
        return !query.Any(IsForbiddenInQuery) && parameters.All(IsNameValuePair) && parameters.Any(IsSignature);
    }

    private static string BearerToken(string token)
    {
        if (token.Length == 0 || token.Any(character => character is < '!' or > '~'))
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.InvalidBearerToken, "The bearer token is empty or holds a character a header value cannot carry.");
        }

        return token;
    }

    private static bool IsForbiddenInQuery(char character) => character is <= ' ' or '#' or > '~';

    private static bool IsNameValuePair(string parameter) => parameter.IndexOf('=', StringComparison.Ordinal) > 0;

    private static bool IsSignature(string parameter) => parameter.StartsWith("sig=", StringComparison.Ordinal);
}
