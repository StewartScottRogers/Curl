using Curl.Api.Options.Http;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2404.c</c>: <see cref="LibPipeWaitHandles"/> over HTTP/2 (<c>CURL_HTTP_VERSION_2_0</c>) with neither the peer
/// nor the host verified, each handle given the stream weight <see cref="FirstStreamWeight"/> plus its index.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from, and its 1 ms waits.</param>
public sealed class Lib2404(TimeProvider timeProvider) : LibPipeWaitHandles(2404, timeProvider)
{
    /// <summary>The first handle's <c>CURLOPT_STREAM_WEIGHT</c>.</summary>
    public const long FirstStreamWeight = 128;

    /// <inheritdoc/>
    protected override IEnumerable<(CurlOption Option, object? Value)> ProgramOptions(LibTestContext context, int index) =>
    [
        (CurlOption.HttpVersion, (long)HttpVersion.Http2),
        (CurlOption.SslVerifypeer, 0L),
        (CurlOption.SslVerifyhost, 0L),
        (CurlOption.StreamWeight, FirstStreamWeight + index),
        .. DefaultVerboseOptions(context),
    ];
}
