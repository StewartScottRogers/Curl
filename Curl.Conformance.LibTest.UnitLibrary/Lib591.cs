using System.Globalization;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib591.c</c>, which cases 592 to 594 run too: a verbose active-mode FTP upload (<c>CURLOPT_FTPPORT</c>
/// <c>-</c>) of the file in <c>libtest_arg3</c> through a multi handle, with the server's connection accepted
/// within the seconds in <c>libtest_arg2</c>, driven by <see cref="LibTestSteps.PerformWithMultiTimeoutUntilDoneAsync"/>
/// with 100 ms when there is no timeout; the program answers the transfer's result. A second argument that is not a
/// number up to 65535 answers <see cref="LibTestErrorCodes.MajorBad"/>; a file that does not open,
/// <see cref="LibTestErrorCodes.FileOpen"/>.
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c>.</param>
public sealed class Lib591(TimeProvider timeProvider) : ILibTest
{
    private const int NoTimeoutMilliseconds = 100;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib591";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryParseAcceptTimeout(context.Argument2, out long acceptTimeout))
        {
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        FileStream upload;
        try
        {
            upload = File.OpenRead(context.Argument3!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            context.StandardError.Write($"fopen() failed with error {exception.Message}\n");
            context.StandardError.Write($"Error opening file '{context.Argument3}'\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.FileOpen);
        }

        await using (upload.ConfigureAwait(false))
        {
            return await LibTestRun.WithMacroLibraryAsync(
                context,
                (steps, library) => TestAsync(context, steps, library, clock, upload, acceptTimeout, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary><c>curlx_str_number(&amp;libtest_arg2, &amp;accept_timeout, 65535)</c>: decimal digits up to 65535.</summary>
    /// <param name="text">The argument, or <see langword="null"/> when the case gives none.</param>
    /// <param name="seconds">The number.</param>
    /// <returns>Whether it is one.</returns>
    public static bool TryParseAcceptTimeout(string? text, out long seconds)
    {
        string digits = new([.. (text ?? string.Empty).TakeWhile(char.IsAsciiDigit)]);
        seconds = digits.Length is > 0 and <= 5 ? long.Parse(digits, CultureInfo.InvariantCulture) : -1;
        return seconds is >= 0 and <= 65535;
    }

    /// <summary>The body after <c>res_global_init</c>: the handles, the options, the loop and the transfer's result.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started.</param>
    /// <param name="upload">The file to upload.</param>
    /// <param name="acceptTimeoutSeconds">The accept timeout in seconds.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        LibTestClock clock,
        Stream upload,
        long acceptTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Verbose, 1L),
                (CurlOption.Url, context.Url),
                (CurlOption.Upload, 1L),
                (CurlOption.Readdata, upload),
                (CurlOption.Ftpport, "-"),
                (CurlOption.AccepttimeoutMs, acceptTimeoutSeconds * 1000),
            ]);
            if (!steps.MultiInit(library, out CurlMulti? multi))
            {
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                await TransferAsync(steps, easy, multi, clock, NoTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// <c>multi_add_handle</c>, the loop, then the result of the first <c>curl_multi_info_read</c> message, as
    /// <c>lib591.c</c> and <c>lib597.c</c> do.
    /// </summary>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle, its options set.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="clock">The program's clock, started.</param>
    /// <param name="noTimeoutMilliseconds">The <c>select()</c> timeout when there is no multi timeout.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the loop ended with no step failing.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> TransferAsync(LibTestSteps steps, CurlEasy easy, CurlMulti multi, LibTestClock clock, int noTimeoutMilliseconds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(multi);
        if (!steps.MultiAddHandle(multi, easy)
            || !await steps.PerformWithMultiTimeoutUntilDoneAsync(multi, clock, noTimeoutMilliseconds, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (multi.InfoRead(out _) is { } message)
        {
            steps.Result = message.Result;
        }

        return true;
    }
}
