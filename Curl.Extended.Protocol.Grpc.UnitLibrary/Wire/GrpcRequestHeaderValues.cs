namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// What a call's Request-Headers carry beyond its <see cref="GrpcRequestTarget" />: the deadline, the <c>user-agent</c>
/// and the <c>-H</c> values that become Custom-Metadata (ADR-1227).
/// </summary>
/// <param name="Timeout">The call's deadline, sent as <c>grpc-timeout</c>, or <see langword="null" /> for none.</param>
/// <param name="UserAgent">The <c>user-agent</c> value, or <see langword="null" /> or empty to send none.</param>
/// <param name="CustomHeaders">Each <c>-H</c> value verbatim, in command-line order.</param>
internal sealed record GrpcRequestHeaderValues(TimeSpan? Timeout, string? UserAgent, IReadOnlyList<string> CustomHeaders)
{
    /// <summary>
    /// The <c>user-agent</c> a call sends when <c>-A</c> is not given: curl's own, as the HTTP handler sends it.
    /// </summary>
    public const string DefaultUserAgent = "curl/8.22.0";

    /// <summary>Gets values that add nothing: no deadline, no <c>user-agent</c> and no Custom-Metadata.</summary>
    public static GrpcRequestHeaderValues None { get; } = new(null, null, []);

    /// <summary>
    /// Gets the <c>user-agent</c> a call sends for an <c>-A</c> value: curl's own when <c>-A</c> is not given, none when
    /// it is empty, else the value itself.
    /// </summary>
    /// <param name="userAgentOption">The <c>-A</c> value, or <see langword="null" /> when it is not given.</param>
    /// <returns>The value to send, or <see langword="null" /> to send none.</returns>
    public static string? UserAgentFor(string? userAgentOption) =>
        userAgentOption switch
        {
            null => DefaultUserAgent,
            "" => null,
            _ => userAgentOption,
        };
}
