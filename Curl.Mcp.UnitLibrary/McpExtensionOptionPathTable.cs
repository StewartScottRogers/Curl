using System.Collections.Frozen;

namespace Curl.Mcp;

/// <summary>
/// How the options the extensions register use a local path (BL-5564, ADR-1311): the extension
/// counterpart of <see cref="McpOptionPathTable" />. An option in <see cref="PathOptionNames" /> names a
/// file or folder the server confines to <c>CURL_MCP_ROOT</c> as <c>-o</c> is; one in
/// <see cref="NoPathOptionNames" /> names none. A value option in neither is confined as a path, so a
/// new extension option fails closed until it is classified here; a drift test in
/// <c>Curl.Mcp.UnitTests</c> walks the extension registry and fails, naming the option, until it is.
/// </summary>
public static class McpExtensionOptionPathTable
{
    /// <summary>Options whose value is a local file or folder (or, for two, a file reference inside the value).</summary>
    internal static readonly FrozenSet<string> PathOptionNames = new[]
    {
        "cache-dir", "compression-dictionary", "dpop", "graphql", "graphql-variables", "grpc-proto", "har",
        "http-file", "npipe", "oauth2-token-cache", "ohttp-keys", "openapi", "powershell", "csharp",
        "record", "replay", "require-sct", "sigstore-bundle", "sigstore-root", "webdav-sync",
        "wireguard", "zsync", "zsync-seed",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Options whose value names no local path: a host, URL, name, number, token or other literal.</summary>
    internal static readonly FrozenSet<string> NoPathOptionNames = new[]
    {
        "checksum", "client-hints", "coap-observe", "connect-rpc", "content-digest", "dane", "dns-json",
        "dns-over-quic", "dns-over-tls", "dns-transport", "graphql-introspect", "graphql-operation",
        "grpc-web", "http-env", "jsonrpc", "masque", "mldsa-verify", "oauth2-authorize-url",
        "oauth2-client-credentials", "oauth2-client-id", "oauth2-client-secret", "oauth2-device",
        "oauth2-device-url", "oauth2-pkce", "oauth2-redirect-port", "oauth2-scope", "odoh-proxy",
        "odoh-target", "ohttp-relay", "openapi-server", "operation", "otel-endpoint", "privacy-pass",
        "quic-migrate", "ratelimit-pace", "resumable-upload", "segments", "sigstore-identity",
        "sigstore-issuer", "sse", "ssh-jump", "structured-fields", "user-from-keychain", "vm-socket",
        "webdav-list", "ws-extended-connect", "ws-repl", "ws-subprotocol",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="longName" /> is classified, as a path option or as one naming no path.</summary>
    /// <param name="longName">The extension option's long name without <c>--</c>.</param>
    /// <returns><see langword="true" /> when this table names it.</returns>
    public static bool IsClassified(string longName) =>
        PathOptionNames.Contains(longName) || NoPathOptionNames.Contains(longName);

    /// <summary>The paths <paramref name="value" />, given to the extension option <paramref name="longName" />, names.</summary>
    /// <param name="longName">The extension option's long name without <c>--</c>.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>The paths to check; none for an option that names none, and for the parts of a value that are not a file.</returns>
    internal static IReadOnlyList<McpValuePath> PathsIn(string longName, string value)
    {
        if (NoPathOptionNames.Contains(longName))
        {
            return [];
        }

        return longName switch
        {
            "graphql-variables" => value.StartsWith('@') ? [new(value[1..])] : [],
            "ohttp-keys" => IsHttpUrl(value) ? [] : [new(value)],
            "oauth2-token-cache" => value.Length == 0 ? [] : [new(value)],
            _ => [new(value)],
        };
    }

    private static bool IsHttpUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
