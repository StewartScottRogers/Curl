using Curl.Extended.Protocol.Grpc.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// Builds the Request-Headers of PROTOCOL-HTTP2.md "Requests", pseudo-headers first as HTTP/2 requires:
/// <c>:method POST</c>, <c>:scheme</c>, <c>:path /Service/Method</c>, <c>:authority</c>, then <c>grpc-timeout</c>
/// straight after them when the call has a deadline, <c>te: trailers</c>, <c>content-type: application/grpc</c>,
/// <c>user-agent</c> when one is given, and last the <c>-H</c> values as Custom-Metadata (ADR-1227).
/// </summary>
/// <remarks>
/// A <c>-H</c> value is read as curl reads one: <c>Name: value</c> sends the value, <c>Name:</c> sends nothing and
/// <c>Name;</c> sends an empty value. One naming a Call-Definition header curl builds (<c>te</c>, <c>grpc-timeout</c>,
/// <c>content-type</c>, <c>user-agent</c>) replaces it in place, or removes it when it sends nothing; a pseudo-header
/// cannot be given. Names go lower case, and a <c>-bin</c> key's value is read as base64 and sent un-padded (ADR-1226).
/// </remarks>
internal static class GrpcRequestHeaders
{
    /// <summary>The content type every gRPC request carries.</summary>
    public const string ContentType = "application/grpc";

    private const string BinarySuffix = "-bin";

    /// <summary>The fields RFC 9113 section 8.2.2 forbids in an HTTP/2 message.</summary>
    private static readonly HashSet<string> ConnectionSpecificNames = ["connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade"];

    /// <summary>
    /// Builds the header list for one call.
    /// </summary>
    /// <param name="target">The method to call and the server to call it on.</param>
    /// <param name="values">The deadline, <c>user-agent</c> and <c>-H</c> values the call sends.</param>
    /// <returns>The header fields, names lower case, in the order they go on the wire.</returns>
    /// <exception cref="ArgumentException">The scheme is not <c>http</c> or <c>https</c>, the authority is empty, or the service or method is empty or holds a <c>/</c>.</exception>
    /// <exception cref="GrpcClientException">A <c>-bin</c> key's value is not base64 (exit 2, curl's usage error).</exception>
    public static IReadOnlyList<KeyValuePair<string, string>> Build(GrpcRequestTarget target, GrpcRequestHeaderValues values)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(values);
        RequireValidTarget(target);
        List<KeyValuePair<string, string>> headers =
        [
            new(":method", "POST"),
            new(":scheme", target.Scheme),
            new(":path", $"/{target.Service}/{target.Method}"),
            new(":authority", target.Authority),
        ];
        if (values.Timeout is { } deadline)
        {
            headers.Add(new("grpc-timeout", GrpcTimeout.Format(deadline)));
        }

        headers.Add(new("te", "trailers"));
        headers.Add(new("content-type", ContentType));
        if (values.UserAgent is { Length: > 0 } userAgent)
        {
            headers.Add(new("user-agent", userAgent));
        }

        List<KeyValuePair<string, string>> metadata = [];
        foreach (string entry in values.CustomHeaders)
        {
            AddCustomHeader(headers, metadata, entry);
        }

        headers.AddRange(metadata);
        return headers;
    }

    /// <summary>
    /// Applies one <c>-H</c> value: to the Call-Definition headers when it names one, otherwise to the Custom-Metadata.
    /// A connection-specific field (RFC 9113 section 8.2.2) is never sent.
    /// </summary>
    private static void AddCustomHeader(List<KeyValuePair<string, string>> callDefinition, List<KeyValuePair<string, string>> metadata, string entry)
    {
        if (ParseCustomHeader(entry) is not (var name, var value))
        {
            return;
        }

        if (IsConnectionSpecific(name, value))
        {
            return;
        }

        int built = callDefinition.FindIndex(header => header.Key == name);
        Store(built >= 0 ? callDefinition : metadata, built, name, value);
    }

    private static bool IsConnectionSpecific(string name, string? value) =>
        ConnectionSpecificNames.Contains(name)
        || (name == "te" && IsOtherThanTrailers(value));

    private static bool IsOtherThanTrailers(string? value) =>
        value is not null && !value.Equals("trailers", StringComparison.OrdinalIgnoreCase);

    private static void Store(List<KeyValuePair<string, string>> list, int index, string name, string? value)
    {
        if (value is null)
        {
            if (index >= 0)
            {
                list.RemoveAt(index);
            }

            return;
        }

        KeyValuePair<string, string> field = new(name, name.EndsWith(BinarySuffix, StringComparison.Ordinal) ? UnpaddedBase64(name, value) : value);
        if (index >= 0)
        {
            list[index] = field;
        }
        else
        {
            list.Add(field);
        }
    }

    /// <summary>
    /// Reads one <c>-H</c> value: its name, lower case and trimmed, and the value it sends (<see langword="null" /> for
    /// none); <see langword="null" /> when it names no header.
    /// </summary>
    private static (string Name, string? Value)? ParseCustomHeader(string entry)
    {
        int colon = entry.IndexOf(':', StringComparison.Ordinal);
        int separator = colon >= 0 ? colon : TrailingSemicolonOf(entry);
        string name = separator > 0 ? entry[..separator].Trim().ToLowerInvariant() : string.Empty;
        if (name.Length == 0)
        {
            return null;
        }

        string value = entry[(separator + 1)..].Trim();
        return (name, colon < 0 || value.Length > 0 ? value : null);
    }

    /// <summary>Gives the index of the <c>;</c> ending a <c>Name;</c> value, or -1 when it does not end so.</summary>
    private static int TrailingSemicolonOf(string entry)
    {
        int semicolon = entry.IndexOf(';', StringComparison.Ordinal);
        return semicolon == entry.Length - 1 ? semicolon : -1;
    }

    private static string UnpaddedBase64(string name, string value)
    {
        string unpadded = value.TrimEnd('=');
        string padded = unpadded.PadRight(unpadded.Length + ((4 - (unpadded.Length % 4)) % 4), '=');
        byte[] bytes = new byte[padded.Length];
        if (!Convert.TryFromBase64String(padded, bytes, out int written))
        {
            throw new GrpcClientException(CurlExitCode.FailedInit, $"The value of the gRPC metadata {name} is not base64");
        }

        return Convert.ToBase64String(bytes, 0, written).TrimEnd('=');
    }

    private static void RequireValidTarget(GrpcRequestTarget target)
    {
        if (target.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"A gRPC call goes over http or https, not '{target.Scheme}'.", nameof(target));
        }

        if (target.Authority.Length == 0)
        {
            throw new ArgumentException("A gRPC call names the server it is sent to.", nameof(target));
        }

        RequireValidPath(target);
    }

    private static void RequireValidPath(GrpcRequestTarget target)
    {
        if (!IsPathSegment(target.Service) || !IsPathSegment(target.Method))
        {
            throw new ArgumentException($"A gRPC service or method name is not empty and holds no '/': '{target.Service}/{target.Method}'.", nameof(target));
        }
    }

    private static bool IsPathSegment(string segment) =>
        segment.Length > 0 && !segment.Contains('/', StringComparison.Ordinal);
}
