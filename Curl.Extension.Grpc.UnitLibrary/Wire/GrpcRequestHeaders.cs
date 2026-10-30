namespace Curl.Extension.Grpc.Wire;

/// <summary>
/// Builds the Request-Headers of PROTOCOL-HTTP2.md "Requests", pseudo-headers first as HTTP/2
/// requires: <c>:method POST</c>, <c>:scheme</c>, <c>:path /Service/Method</c>, <c>:authority</c>,
/// <c>te: trailers</c>, <c>grpc-timeout</c> when the call has a deadline,
/// <c>content-type: application/grpc</c> and <c>user-agent</c> when one is given.
/// </summary>
internal static class GrpcRequestHeaders
{
    /// <summary>The content type every gRPC request carries.</summary>
    public const string ContentType = "application/grpc";

    /// <summary>
    /// Builds the header list for one call.
    /// </summary>
    /// <param name="target">The method to call and the server to call it on.</param>
    /// <param name="timeout">The call's deadline, or <see langword="null" /> for none.</param>
    /// <param name="userAgent">The <c>user-agent</c> value, or <see langword="null" /> to send none.</param>
    /// <returns>The header fields, names lower case, in the order they go on the wire.</returns>
    /// <exception cref="ArgumentException">The scheme is not <c>http</c> or <c>https</c>, the authority is empty, or the service or method is empty or holds a <c>/</c>.</exception>
    public static IReadOnlyList<KeyValuePair<string, string>> Build(GrpcRequestTarget target, TimeSpan? timeout, string? userAgent)
    {
        ArgumentNullException.ThrowIfNull(target);
        RequireValidTarget(target);
        List<KeyValuePair<string, string>> headers =
        [
            new(":method", "POST"),
            new(":scheme", target.Scheme),
            new(":path", $"/{target.Service}/{target.Method}"),
            new(":authority", target.Authority),
            new("te", "trailers"),
        ];
        if (timeout is { } deadline)
        {
            headers.Add(new("grpc-timeout", GrpcTimeout.Format(deadline)));
        }

        headers.Add(new("content-type", ContentType));
        if (userAgent is not null)
        {
            headers.Add(new("user-agent", userAgent));
        }

        return headers;
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
