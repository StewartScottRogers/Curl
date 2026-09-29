namespace Curl.Protocol.Rtsp;

/// <summary>An RTSP request method, by the name written on the request line (ADR-0169).</summary>
/// <param name="Name">The method name, such as <c>OPTIONS</c>.</param>
/// <remarks>
/// The curl 8.21.0 tool only ever sends <see cref="Options" />: <c>-X</c> does not change the
/// RTSP method, and the other methods are libcurl-only. The method is still a value the request
/// formatter takes, so they can be added without reshaping the code.
/// </remarks>
internal sealed record RtspMethod(string Name)
{
    /// <summary>Gets <c>OPTIONS</c>, the one request the curl tool makes per transfer.</summary>
    internal static RtspMethod Options { get; } = new("OPTIONS");
}
