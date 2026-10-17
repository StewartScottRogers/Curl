using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary><c>lib502.c</c>: one URL through a multi handle, driven by <c>multi_perform</c> and a 100 ms <c>select()</c> until it ends.</summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib502(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib502";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(
            context,
            (steps, library) => LibTestRun.SingleMultiTransferAsync(steps, library, clock, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)], 100, cancellationToken));
    }
}
