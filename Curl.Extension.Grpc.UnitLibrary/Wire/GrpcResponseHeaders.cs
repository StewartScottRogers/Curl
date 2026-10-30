using System.Globalization;

namespace Curl.Extension.Grpc.Wire;

/// <summary>
/// Reads PROTOCOL-HTTP2.md "Responses": the Response-Headers (<c>:status</c>, <c>content-type</c>),
/// a Trailers-Only response, and the Trailers (<c>grpc-status</c>, <c>grpc-message</c>).
/// </summary>
internal static class GrpcResponseHeaders
{
    private static readonly Dictionary<int, GrpcStatusCode> CodesForHttpStatus = new()
    {
        [400] = GrpcStatusCode.Internal,
        [401] = GrpcStatusCode.Unauthenticated,
        [403] = GrpcStatusCode.PermissionDenied,
        [404] = GrpcStatusCode.Unimplemented,
        [429] = GrpcStatusCode.Unavailable,
        [502] = GrpcStatusCode.Unavailable,
        [503] = GrpcStatusCode.Unavailable,
        [504] = GrpcStatusCode.Unavailable,
    };

    /// <summary>
    /// Reads the response's first HEADERS. A <c>grpc-status</c> there makes it Trailers-Only; failing
    /// that, an HTTP status other than 200 ends the call with the code http-grpc-status-mapping.md gives
    /// it; otherwise the content type must be <c>application/grpc</c> or one of its <c>+format</c> forms.
    /// </summary>
    /// <param name="headers">The header fields as received.</param>
    /// <returns>The HTTP status and, when the call ended here, its status.</returns>
    /// <exception cref="GrpcWireException">The <c>:status</c> is missing or not a number, or a 200 response is not <c>application/grpc</c>.</exception>
    public static GrpcResponseHead ReadHeaders(IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var httpStatusText = FindValue(headers, ":status");
        if (!int.TryParse(httpStatusText, NumberStyles.None, CultureInfo.InvariantCulture, out var httpStatus))
        {
            throw new GrpcWireException($"A gRPC response's :status is '{httpStatusText}', not a status code.");
        }

        if (FindValue(headers, "grpc-status") is not null)
        {
            return new GrpcResponseHead(httpStatus, true, ReadStatus(headers));
        }

        if (httpStatus != 200)
        {
            var code = CodesForHttpStatus.GetValueOrDefault(httpStatus, GrpcStatusCode.Unknown);
            return new GrpcResponseHead(httpStatus, false, new GrpcStatus(code, $"HTTP status {httpStatus}"));
        }

        RequireGrpcContentType(FindValue(headers, "content-type"));
        return new GrpcResponseHead(httpStatus, false, null);
    }

    /// <summary>
    /// Reads the Trailers that end a call.
    /// </summary>
    /// <param name="trailers">The trailer fields as received.</param>
    /// <returns>The call's status.</returns>
    /// <exception cref="GrpcWireException">The trailers carry no <c>grpc-status</c>.</exception>
    public static GrpcStatus ReadTrailers(IReadOnlyList<KeyValuePair<string, string>> trailers)
    {
        if (FindValue(trailers, "grpc-status") is null)
        {
            throw new GrpcWireException("A gRPC response's trailers carry no grpc-status.");
        }

        return ReadStatus(trailers);
    }

    private static GrpcStatus ReadStatus(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var statusText = FindValue(fields, "grpc-status");
        var message = GrpcStatusMessage.Decode(FindValue(fields, "grpc-message") ?? string.Empty);
        return int.TryParse(statusText, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            ? new GrpcStatus((GrpcStatusCode)code, message)
            : new GrpcStatus(GrpcStatusCode.Internal, $"malformed grpc-status '{statusText}'");
    }

    private static void RequireGrpcContentType(string? contentType)
    {
        if (contentType is null || !IsGrpcContentType(contentType))
        {
            throw new GrpcWireException($"A gRPC response's content-type is '{contentType}', not application/grpc.");
        }
    }

    private static bool IsGrpcContentType(string contentType)
    {
        var grpc = GrpcRequestHeaders.ContentType;
        return contentType.StartsWith(grpc, StringComparison.OrdinalIgnoreCase)
            && (contentType.Length == grpc.Length || contentType[grpc.Length] is '+' or ';');
    }

    private static string? FindValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name)
    {
        foreach (var field in fields)
        {
            if (string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return field.Value;
            }
        }

        return null;
    }
}
