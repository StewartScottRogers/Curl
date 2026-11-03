using System.Text.Json;

namespace Curl.Conformance.Extended.McpClient;

/// <summary>One HTTP request curl sent the Streamable HTTP reference server.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Target">The request target.</param>
/// <param name="Headers">The header fields, looked up without regard to case.</param>
/// <param name="Body">The body, decoded as UTF-8; empty when there is none.</param>
public sealed record McpHttpRequestRecord(string Method, string Target, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>Gets the JSON-RPC method of the message the body carries, or <see langword="null" /> without a body.</summary>
    public string? JsonRpcMethod => Body.Length == 0 ? null : JsonDocument.Parse(Body).RootElement.GetProperty("method").GetString();

    /// <summary>Finds a header field's value.</summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The value, or <see langword="null" /> when the request has no such field.</returns>
    public string? Header(string name) => Headers.GetValueOrDefault(name);
}
