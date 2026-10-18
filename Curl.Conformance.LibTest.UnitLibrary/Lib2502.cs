using Curl.Api.Callbacks;
using Curl.Api.Options.Http;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2502.c</c>: <see cref="LibPipeWaitHandles"/> over HTTP/3 only (<c>CURL_HTTP_VERSION_3ONLY</c>), each handle
/// with a 5-second connect timeout, the CA file <c>libtest_arg4</c> and <c>libtest_debug_cb</c> tracing to stderr
/// without hex or times.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from, and its 1 ms waits.</param>
public sealed class Lib2502(TimeProvider timeProvider) : LibPipeWaitHandles(2502, timeProvider)
{
    /// <summary>The <c>CURLOPT_CONNECTTIMEOUT_MS</c> each handle gets.</summary>
    public const long ConnectTimeoutMilliseconds = 5000;

    /// <inheritdoc/>
    protected override IEnumerable<(CurlOption Option, object? Value)> ProgramOptions(LibTestContext context, int index)
    {
        ArgumentNullException.ThrowIfNull(context);
        var trace = new LibTestDebugTrace(context.StandardError, TimeProvider, traceTime: false, noHex: true);
        return
        [
            (CurlOption.HttpVersion, (long)HttpVersion.Http3Only),
            (CurlOption.ConnecttimeoutMs, ConnectTimeoutMilliseconds),
            (CurlOption.Cainfo, context.Argument4),
            (CurlOption.Debugdata, trace),
            (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
        ];
    }
}
