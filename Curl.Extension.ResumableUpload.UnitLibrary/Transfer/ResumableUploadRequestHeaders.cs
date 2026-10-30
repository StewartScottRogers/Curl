using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Builds a resumable upload request's header fields: <c>Host</c>, the flow's own, then <c>-H</c>'s.</summary>
internal static class ResumableUploadRequestHeaders
{
    /// <summary>Gives the header fields of a request to <paramref name="uri" />.</summary>
    /// <param name="uri">Where the request goes; its authority is the <c>Host</c> field (RFC 9112 §3.2).</param>
    /// <param name="own">The fields the upload flow sets.</param>
    /// <param name="context">The transfer, whose <c>-H</c> lines follow, except one naming a field already set or <c>Content-Length</c>, and one with no value.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> For(Uri uri, IReadOnlyList<KeyValuePair<string, string>> own, ITransferContext context)
    {
        List<KeyValuePair<string, string>> headers = [new("Host", uri.Authority), .. own];
        foreach (var line in context.Http?.Headers ?? [])
        {
            if (FieldOf(line) is { } field && !IsSet(headers, field.Key))
            {
                headers.Add(field);
            }
        }

        return headers;
    }

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
