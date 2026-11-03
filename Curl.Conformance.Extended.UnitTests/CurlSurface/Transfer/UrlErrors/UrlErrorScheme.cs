namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>
/// One extended scheme as the URL-error checks drive it: how its URL is built around the authority a check puts in it,
/// and where the transfer connects when the host is not the URL's own.
/// </summary>
/// <param name="Scheme">The URL scheme.</param>
/// <param name="Path">The URL's path, one the scheme accepts, so the URL fails only for what the check puts in its authority.</param>
/// <param name="UserInfo">What goes before the authority's host, such as <c>fs@</c> for a Data Lake file system; empty when nothing does.</param>
/// <param name="EndpointVariable">
/// The environment variable that names the endpoint the transfer connects to, such as <c>AWS_ENDPOINT_URL</c>, for a
/// scheme whose host names a bucket rather than a server; <see langword="null" /> when the transfer connects to the URL's host.
/// </param>
/// <param name="RefusedTarget">The <c>host:port</c> curl names when the connection is refused.</param>
/// <param name="Datagram">Whether the transfer runs over UDP, where the harness's datagram connector reports a port with no route.</param>
/// <param name="Helper">Whether a <c>curl-proto-&lt;scheme&gt;</c> helper serves the scheme (ADR-1142), so the run puts one on PATH.</param>
internal sealed record UrlErrorScheme(
    string Scheme,
    string Path = "/x",
    string UserInfo = "",
    string? EndpointVariable = null,
    string RefusedTarget = "localhost:1",
    bool Datagram = false,
    bool Helper = false)
{
    /// <summary>The URL with <paramref name="authority" /> as its host and port.</summary>
    public string Url(string authority) => $"{Scheme}://{UserInfo}{authority}{Path}";

    /// <summary>The URL and environment of a transfer that connects to <paramref name="authority" />.</summary>
    public (string Url, Dictionary<string, string> Environment) Reaching(string authority) =>
        EndpointVariable is null
            ? (Url(authority), [])
            : (Url("bucket"), new() { [EndpointVariable] = $"http://{authority}" });

    /// <summary>The requirement ID of <paramref name="check" /> for this scheme.</summary>
    public string Requirement(string check) => $"CurlSurface:{check}-{Scheme}-1";
}
