using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Builds a resumable upload request's header fields: <c>Host</c>, the flow's own, then <c>-H</c>'s.</summary>
internal static class ResumableUploadRequestHeaders
{
    /// <summary>Gives the header fields of a request to <paramref name="uri" />.</summary>
    /// <param name="uri">Where the request goes; its authority is the <c>Host</c> field (RFC 9112 §3.2).</param>
    /// <param name="origin">The transfer's URL: a request to another origin (scheme, host or port) gets no <c>Authorization</c>, <c>Cookie</c> or <c>Proxy-Authorization</c> from <c>-H</c>, as curl drops them on a cross-origin redirect.</param>
    /// <param name="own">The fields the upload flow sets.</param>
    /// <param name="context">The transfer, whose <c>-H</c> lines follow, except one naming a field already set or <c>Content-Length</c>, and one with no value.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> For(Uri uri, Uri origin, IReadOnlyList<KeyValuePair<string, string>> own, ITransferContext context)
    {
        List<KeyValuePair<string, string>> headers = [new("Host", uri.Authority), .. own];
        var crossOrigin = !SameOrigin(uri, origin);
        foreach (var line in context.Http?.Headers ?? [])
        {
            if (FieldOf(line) is { } field && IsSent(headers, field.Key, crossOrigin))
            {
                headers.Add(field);
            }
        }

        return headers;
    }

    private static bool IsSent(List<KeyValuePair<string, string>> headers, string name, bool crossOrigin) =>
        !IsSet(headers, name) && !(crossOrigin && IsCredential(name));

    private static bool SameOrigin(Uri uri, Uri origin) =>
        uri.Scheme == origin.Scheme && uri.Port == origin.Port && string.Equals(uri.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase);

    private static bool IsCredential(string name) =>
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase);

    private static KeyValuePair<string, string>? FieldOf(string line)
    {
        var colon = line.IndexOf(':');
        var value = colon > 0 ? line[(colon + 1)..].Trim() : string.Empty;
        return value.Length > 0 ? new(line[..colon].Trim(), value) : null;
    }

    private static bool IsSet(List<KeyValuePair<string, string>> headers, string name) =>
        name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
        || headers.Exists(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
}
