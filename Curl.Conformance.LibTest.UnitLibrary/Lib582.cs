namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib582.c</c>: an SFTP upload of the file in <c>libtest_arg2</c> as the user in <c>libtest_arg3</c> with the
/// key files in the fourth and fifth arguments, driven by <c>curl_multi_socket_action</c> from the sets its socket
/// callback keeps and the deadline its timer callback sets (<see cref="Lib582Run"/>). Fewer than four arguments
/// answer <see cref="LibTestErrorCodes.MajorBad"/>; a file that does not open, <see cref="LibTestErrorCodes.FileOpen"/>.
/// </summary>
/// <param name="timeProvider">The program's clock, for the timer deadline and <c>abort_on_test_timeout</c>.</param>
public sealed class Lib582(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib582";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Arguments.Count < 4)
        {
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        FileStream source;
        try
        {
            source = File.OpenRead(context.Argument2!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.StandardError.Write($"fopen() failed with error {exception.Message}\n");
            context.StandardError.Write($"Error opening file '{context.Argument2}'\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.FileOpen);
        }

        await using (source.ConfigureAwait(false))
        {
            context.StandardError.Write($"Set to upload {source.Length} bytes\n");
            var run = new Lib582Run(context, timeProvider, clock, source);
            return await LibTestRun.WithMacroLibraryAsync(context, (steps, library) => run.TestAsync(steps, library, cancellationToken)).ConfigureAwait(false);
        }
    }
}
