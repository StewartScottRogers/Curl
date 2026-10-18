using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1541.c</c>: a GET whose header callback, at the first header, checks that the connect, pretransfer,
/// starttransfer and total times are above 0 and the appconnect time and download speed are 0, and that once the
/// transfer ends every time but appconnect is above 0; each check writes <c>CURLINFO_X on &lt;where&gt; is OK</c> or
/// <c>is WRONG: N</c> on stdout, with the headers and body. The transfer's code is the program's.
/// </summary>
/// <param name="timeProvider">The clock behind <c>start_test_timing</c>.</param>
public sealed class Lib1541(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The <c>lib1541.c</c> line of <c>check_time</c>'s <c>t1541_geterr</c> call.</summary>
    public const int CheckTimeErrorLine = 56;

    /// <summary>The <c>lib1541.c</c> line of <c>check_time0</c>'s <c>t1541_geterr</c> call.</summary>
    public const int CheckTimeZeroErrorLine = 68;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1541";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        new LibTestClock(timeProvider).StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            Stream output = context.StandardOutput;
            int headers = 0;
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Url, context.Url),
                (CurlOption.Writefunction, (WriteCallback)(data =>
                {
                    output.Write(data);
                    return data.Length;
                })),
                (CurlOption.Headerfunction, (HeaderCallback)(header =>
                {
                    if (headers++ == 0)
                    {
                        CheckFirstHeader(easy, output);
                    }

                    output.Write(header);
                    return header.Length;
                })),
                (CurlOption.Noprogress, 0L),
            ]);

            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            CheckDone(easy, output);
        }
    }

    /// <summary>The checks at the first header (see curl issue #13125).</summary>
    /// <param name="easy">The handle.</param>
    /// <param name="output">The program's stdout.</param>
    public static void CheckFirstHeader(CurlEasy easy, Stream output)
    {
        const string Where = "1st header";
        CheckTime(easy, output, CurlInfo.ConnectTimeT, "CURLINFO_CONNECT_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.PretransferTimeT, "CURLINFO_PRETRANSFER_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.StarttransferTimeT, "CURLINFO_STARTTRANSFER_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.TotalTimeT, "CURLINFO_TOTAL_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.AppconnectTimeT, "CURLINFO_APPCONNECT_TIME_T", Where, mustBeZero: true);
        CheckTime(easy, output, CurlInfo.SpeedDownloadT, "CURLINFO_SPEED_DOWNLOAD_T", Where, mustBeZero: true);
    }

    /// <summary>The checks once the transfer ends.</summary>
    /// <param name="easy">The handle.</param>
    /// <param name="output">The program's stdout.</param>
    public static void CheckDone(CurlEasy easy, Stream output)
    {
        const string Where = "done";
        CheckTime(easy, output, CurlInfo.ConnectTimeT, "CURLINFO_CONNECT_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.PretransferTimeT, "CURLINFO_PRETRANSFER_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.PosttransferTimeT, "CURLINFO_POSTTRANSFER_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.StarttransferTimeT, "CURLINFO_STARTTRANSFER_TIME_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.AppconnectTimeT, "CURLINFO_APPCONNECT_TIME_T", Where, mustBeZero: true);
        CheckTime(easy, output, CurlInfo.SpeedDownloadT, "CURLINFO_SPEED_DOWNLOAD_T", Where, mustBeZero: false);
        CheckTime(easy, output, CurlInfo.TotalTimeT, "CURLINFO_TOTAL_TIME_T", Where, mustBeZero: false);
    }

    /// <summary>
    /// <c>check_time</c> (<paramref name="mustBeZero"/> <see langword="false"/>: above 0) or <c>check_time0</c>
    /// (<see langword="true"/>: 0), writing <c>report_time</c>'s line, or <c>t1541_geterr</c>'s when the info fails.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="info">The time to read.</param>
    /// <param name="name">Its name, such as <c>CURLINFO_CONNECT_TIME_T</c>.</param>
    /// <param name="where">When it is read: <c>1st header</c> or <c>done</c>.</param>
    /// <param name="mustBeZero">Whether the time must be 0 rather than above 0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    public static void CheckTime(CurlEasy easy, Stream output, CurlInfo info, string name, string where, bool mustBeZero)
    {
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(output);
        CurlCode result = easy.GetInfo(info, out object? value);
        string line;
        if (result != CurlCode.Ok)
        {
            int lineNumber = mustBeZero ? CheckTimeZeroErrorLine : CheckTimeErrorLine;
            // Upstream's KN() hands the whole name to a "CURLINFO_%s" format, so the prefix is doubled.
            line = $"CURLINFO_{name} returned {(int)result}, \"{CurlErrorText.Easy(result)}\" on line {lineNumber}\n";
        }
        else
        {
            long time = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            bool ok = mustBeZero ? time == 0 : time > 0;
            line = ok ? $"{name} on {where} is OK\n" : $"{name} on {where} is WRONG: {time}\n";
        }

        output.Write(System.Text.Encoding.ASCII.GetBytes(line));
    }
}
