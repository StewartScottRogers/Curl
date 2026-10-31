using System.Security.Cryptography;
using System.Text.Json;

namespace Curl.Extension.CertificateTransparency;

/// <summary>
/// Reads the logs' public keys from a log list in the version 3 JSON format Chrome and Apple publish
/// (<c>https://www.gstatic.com/ct/log_list/v3/log_list.json</c>): every <c>operators[].logs[].key</c>, a base64 DER
/// <c>SubjectPublicKeyInfo</c>. Each key is filed under its log ID, the SHA-256 hash of that key (RFC 6962 §3.2), worked
/// out here rather than taken from the list's <c>log_id</c>.
/// </summary>
internal static class CertificateTransparencyLogList
{
    /// <summary>Reads a log list.</summary>
    /// <param name="json">The list's UTF-8 JSON.</param>
    /// <returns>Each log's DER public key, by its log ID in upper-case hexadecimal.</returns>
    /// <exception cref="JsonException">The list is not JSON.</exception>
    /// <exception cref="FormatException">A key is not base64.</exception>
    internal static IReadOnlyDictionary<string, byte[]> KeysByLogId(ReadOnlyMemory<byte> json)
    {
        using var document = JsonDocument.Parse(json);
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var log in LogsIn(document.RootElement))
        {
            if (log.ValueKind == JsonValueKind.Object && log.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String)
            {
                var publicKey = Convert.FromBase64String(key.GetString()!);
                keys[Convert.ToHexString(SHA256.HashData(publicKey))] = publicKey;
            }
        }

        return keys;
    }

    private static IEnumerable<JsonElement> LogsIn(JsonElement root) =>
        ArrayIn(root, "operators").SelectMany(operatorElement => ArrayIn(operatorElement, "logs"));

    private static IEnumerable<JsonElement> ArrayIn(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];
}
