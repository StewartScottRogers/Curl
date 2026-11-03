using System.Text;
using System.Xml.Linq;

namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>One response an Azure Storage reference endpoint answers a request with; the writer adds the framing.</summary>
/// <param name="StatusCode">The status code.</param>
/// <param name="ReasonPhrase">The reason phrase.</param>
/// <param name="Headers">The header fields, before the framing field.</param>
/// <param name="Body">The body.</param>
public sealed record AzureStorageHttpResponse(int StatusCode, string ReasonPhrase, IReadOnlyList<KeyValuePair<string, string>> Headers, byte[] Body)
{
    /// <summary>The service version every response names in <c>x-ms-version</c>, the one curl sends.</summary>
    public const string ServiceVersion = "2025-01-05";

    /// <summary>A successful response with <c>x-ms-version</c> and <paramref name="headers" />.</summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="reasonPhrase">The reason phrase.</param>
    /// <param name="body">The body.</param>
    /// <param name="headers">The header fields after <c>x-ms-version</c>.</param>
    /// <returns>The response.</returns>
    public static AzureStorageHttpResponse Success(int statusCode, string reasonPhrase, byte[] body, params IReadOnlyList<KeyValuePair<string, string>> headers) =>
        new(statusCode, reasonPhrase, [new("x-ms-version", ServiceVersion), .. headers], body);

    /// <summary>
    /// An error response ("Status and error codes"): <c>x-ms-error-code</c> names <paramref name="errorCode" />, and the
    /// body is an <c>Error</c> element holding <c>Code</c> and <c>Message</c>.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="reasonPhrase">The reason phrase, which is also the error message.</param>
    /// <param name="errorCode">The error code, such as <c>BlobNotFound</c>.</param>
    /// <returns>The response.</returns>
    public static AzureStorageHttpResponse Error(int statusCode, string reasonPhrase, string errorCode)
    {
        XElement error = new("Error", new XElement("Code", errorCode), new XElement("Message", reasonPhrase));
        byte[] body = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>" + error.ToString(SaveOptions.DisableFormatting));
        return Success(statusCode, reasonPhrase, body, new("Content-Type", "application/xml"), new("x-ms-error-code", errorCode));
    }
}
