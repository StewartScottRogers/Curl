using System.Text.Json;
using Curl.Extension.Protocol.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.OciRegistry.Client;

/// <summary>
/// Turns a registry's error response into a failed pull or push (OCI Distribution Specification 1.1, "Error Codes"): the first
/// <c>errors[].code</c> and <c>message</c> of its body join curl's <c>-f</c> message, and <c>UNAUTHORIZED</c>,
/// <c>DENIED</c>, a 401 or a 403 fail as a denied login (exit 67), every other error as curl's <c>-f</c> does (exit 22).
/// </summary>
internal static class OciRegistryError
{
    /// <summary>Creates the failure for <paramref name="response"/>.</summary>
    /// <param name="response">The registry's response, whose status is not 2xx.</param>
    /// <returns>The failure to throw.</returns>
    public static OciTransferException From(OciHttpResponse response)
    {
        var (code, message) = ReadFirstError(response.Body);
        var detail = code is null ? string.Empty : $" ({code}: {message})";
        return new OciTransferException(ExitCodeFor(code, response.StatusCode), $"The requested URL returned error: {response.StatusCode}{detail}");
    }

    private static CurlExitCode ExitCodeFor(string? code, int statusCode) =>
        IsDeniedCode(code) || statusCode is 401 or 403 ? CurlExitCode.LoginDenied : CurlExitCode.HttpReturnedError;

    private static bool IsDeniedCode(string? code) =>
        string.Equals(code, "UNAUTHORIZED", StringComparison.Ordinal) || string.Equals(code, "DENIED", StringComparison.Ordinal);

    /// <summary>Reads the first entry of the body's <c>errors</c> array.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>Its <c>code</c> and <c>message</c>, each <see langword="null"/> when the body does not hold it as a string.</returns>
    internal static (string? Code, string? Message) ReadFirstError(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return FindFirstError(document.RootElement) is { } error
                ? (ReadString(error, "code"), ReadString(error, "message"))
                : (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static JsonElement? FindFirstError(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("errors", out var errors)
        && errors.ValueKind == JsonValueKind.Array
        && errors.GetArrayLength() > 0
        && errors[0].ValueKind == JsonValueKind.Object
            ? errors[0]
            : null;

    private static string? ReadString(JsonElement error, string name) =>
        error.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String ? member.GetString() : null;
}
