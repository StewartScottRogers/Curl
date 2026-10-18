using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib560.c</c>: an HTTPS download with headers on and neither the peer nor the host verified, through a
/// multi handle's <c>select()</c> loop of one second. Upstream lets <c>curl_easy_init</c> call
/// <c>curl_global_init</c>; the port inits the library as <c>global_init</c> does, which says nothing unless it fails.
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c>.</param>
public sealed class Lib560(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib560";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(
            context,
            (steps, library) => LibTestRun.SingleMultiTransferAsync(
                steps,
                library,
                clock,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Header, 1L),
                    (CurlOption.SslVerifypeer, 0L),
                    (CurlOption.SslVerifyhost, 0L),
                ],
                1000,
                cancellationToken));
    }
}
