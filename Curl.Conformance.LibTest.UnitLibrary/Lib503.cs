using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib503.c</c>: one URL through a multi handle, through the proxy the second argument names with
/// <c>CURLOPT_HTTPPROXYTUNNEL</c>, user <c>test:ing</c> and proxy user <c>test%20</c> / <c>ing%41</c>, headers and
/// verbose on, driven by <c>multi_perform</c> and a one-second <c>select()</c> until it ends.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib503(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib503";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(
            context,
            (steps, library) => LibTestRun.SingleMultiTransferAsync(steps, library, clock, OptionsOf(context), 1000, cancellationToken));
    }

    private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Proxy, context.Argument2),
        (CurlOption.Url, context.Url),
        (CurlOption.Userpwd, "test:ing"),
        (CurlOption.Proxyusername, "test%20"),
        (CurlOption.Proxypassword, "ing%41"),
        (CurlOption.Httpproxytunnel, 1L),
        (CurlOption.Header, 1L),
        (CurlOption.Verbose, 1L),
    ];
}
