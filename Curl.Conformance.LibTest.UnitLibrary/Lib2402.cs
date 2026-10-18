using Curl.Api.Options.Http;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2402.c</c>: <see cref="LibPipeWaitHandles"/> over HTTP/2 (<c>CURL_HTTP_VERSION_2_0</c>) with neither the peer
/// nor the host verified, tracing to stderr as <see cref="LibPipeWaitHandles.DefaultVerboseOptions"/> does.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from, and its 1 ms waits.</param>
public sealed class Lib2402(TimeProvider timeProvider) : LibPipeWaitHandles(2402, timeProvider)
{
    /// <inheritdoc/>
    protected override IEnumerable<(CurlOption Option, object? Value)> ProgramOptions(LibTestContext context, int index) =>
    [
        (CurlOption.HttpVersion, (long)HttpVersion.Http2),
        (CurlOption.SslVerifypeer, 0L),
        (CurlOption.SslVerifyhost, 0L),
        .. DefaultVerboseOptions(context),
    ];
}
