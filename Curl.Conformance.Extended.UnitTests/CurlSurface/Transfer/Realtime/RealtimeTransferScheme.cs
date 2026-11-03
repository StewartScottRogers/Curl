using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>How the server of a realtime scheme's transfer is reached: in clear, over the harness's TLS route, or over TLS that selects ALPN <c>h2</c>.</summary>
internal enum RealtimeTransport
{
    Clear,
    Tls,
    TlsAlpnH2,
}

/// <summary>
/// One realtime scheme as the transfer-surface checks drive it: the URL, the server that answers the transfer with
/// <see cref="Body" />, and the server that fails it with exit 22 and <see cref="FailureMessage" />.
/// </summary>
/// <param name="Scheme">The URL scheme.</param>
/// <param name="Port">The port the URL names and the route serves.</param>
/// <param name="Transport">How the route serves the connection.</param>
/// <param name="Path">The URL's path, whose last segment <c>-O</c> names the file after.</param>
/// <param name="RemoteName">The file name <c>-O</c> writes.</param>
/// <param name="Answering">Makes the server that answers the transfer.</param>
/// <param name="AnsweringArguments">The arguments before the URL that make the answered transfer, given a temporary directory.</param>
/// <param name="Body">What the answered transfer writes.</param>
/// <param name="Failing">Makes the server that fails the transfer.</param>
/// <param name="FailingArguments">The arguments before the URL that make the failed transfer, given a temporary directory.</param>
/// <param name="FailureMessage">The error message of the failed transfer, after <c>curl: (22) </c>.</param>
/// <param name="FailureBody">What the failed transfer writes before it fails; empty when the server error carries no body.</param>
internal sealed record RealtimeTransferScheme(
    string Scheme,
    int Port,
    RealtimeTransport Transport,
    string Path,
    string RemoteName,
    Func<IReferenceServer> Answering,
    Func<string, Task<string[]>> AnsweringArguments,
    string Body,
    Func<IReferenceServer> Failing,
    Func<string, Task<string[]>> FailingArguments,
    string FailureMessage,
    string FailureBody = "")
{
    public string Url => $"{Scheme}://localhost:{Port}{Path}";

    /// <summary>The requirement ID of <paramref name="check" /> for this scheme.</summary>
    public string Requirement(string check) => $"CurlSurface:{check}-{Scheme}-1";

    public static Task<string[]> NoArguments(string directory) => Task.FromResult<string[]>([]);
}
