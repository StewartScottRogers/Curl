using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib525.c</c>: an active-FTP upload (<c>CURLOPT_FTPPORT "-"</c>) of the file the second argument names
/// through a multi handle, driven by <c>multi_perform</c> and a one-second <c>select()</c> until it ends. No
/// second argument answers <see cref="LibTestErrorCodes.Usage"/>, a file that cannot be opened
/// <see cref="LibTestErrorCodes.FileOpen"/>; the <c>fopen()</c> line carries the .NET exception's message in
/// place of the C library's <c>errno</c> text, and the <c>fstat</c> check is left out, as it cannot fail once
/// the file is open. Cleanup type PA for test 529, PB for every other.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib525(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib525";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        TextWriter error = context.StandardError;
        if (context.Argument2 is not { } path)
        {
            error.Write("Usage: test [url] [uploadfile]\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.Usage);
        }

        FileStream source;
        try
        {
            source = File.OpenRead(path);
        }
        catch (IOException exception)
        {
            error.Write($"fopen() failed with error {exception.Message}\n");
            error.Write($"Error opening file '{path}'\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.FileOpen);
        }

        await using (source.ConfigureAwait(false))
        {
            return await LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, source, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="source">The open file to upload.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(source);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        CurlMulti? multi = null;
        if (steps.MultiInit(library, out multi) && steps.SetOptionsAndAddHandle(easy, OptionsOf(context, source), multi))
        {
            await steps.PerformUntilDoneAsync(multi, clock, 1000, cancellationToken).ConfigureAwait(false);
        }

        await CleanUpAsync(context.TestNumber, multi, easy).ConfigureAwait(false);
    }

    private static async ValueTask CleanUpAsync(int testNumber, CurlMulti? multi, CurlEasy easy)
    {
        multi?.RemoveHandle(easy);
        if (testNumber == 529)
        {
            await (multi?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
            await easy.DisposeAsync().ConfigureAwait(false);
            return;
        }

        await easy.DisposeAsync().ConfigureAwait(false);
        await (multi?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
    }

    private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context, Stream source) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Upload, 1L),
        (CurlOption.Url, context.Url),
        (CurlOption.Verbose, 1L),
        (CurlOption.Ftpport, "-"),
        (CurlOption.Readdata, source),
        (CurlOption.InfilesizeLarge, source.Length),
    ];
}
