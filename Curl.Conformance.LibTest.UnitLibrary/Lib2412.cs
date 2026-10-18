using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2412.c</c>: a verbose handle with no URL, traced by <c>libtest_debug_cb</c> with times and without hex, added
/// to a multi handle that has not run, whose <c>curl_multi_fdset</c> must give no descriptor: <c>There are no file
/// descriptors to wait for</c> on stderr, or the descriptor it gave and <see cref="LibTestErrorCodes.Failure"/>. A
/// failing <c>curl_multi_init</c>, <c>curl_easy_init</c>, <c>curl_multi_add_handle</c> or <c>curl_multi_fdset</c>
/// writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
/// <param name="timeProvider">The time the trace lines start with.</param>
public sealed class Lib2412(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib2412";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">The time the trace lines start with.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInitOrMajorBad(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
                _ = easy.SetOption(CurlOption.Debugdata, trace);
                _ = easy.SetOption(CurlOption.Debugfunction, (DebugCallback)trace.Trace);
                _ = easy.SetOption(CurlOption.Verbose, 1L);
                CheckNoDescriptor(context, steps, multi, easy);
                _ = multi.RemoveHandle(easy);
            }
        }
    }

    private static void CheckNoDescriptor(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlEasy easy)
    {
        CurlMultiCode code = multi.AddHandle(easy);
        if (code == CurlMultiCode.Ok)
        {
            code = multi.FdSet(new HashSet<CurlSocket>(), new HashSet<CurlSocket>(), new HashSet<CurlSocket>(), out nint maxSocket);
            if (code == CurlMultiCode.Ok)
            {
                WriteDescriptor(context, steps, maxSocket);
                return;
            }

            context.StandardError.Write($"curl_multi_fdset() failed: {(int)code}\n");
        }
        else
        {
            context.StandardError.Write($"curl_multi_add_handle() failed: {(int)code}\n");
        }

        steps.Result = LibTestErrorCodes.MajorBad;
    }

    private static void WriteDescriptor(LibTestContext context, LibTestSteps steps, nint maxSocket)
    {
        if (maxSocket == -1)
        {
            context.StandardError.Write("There are no file descriptors to wait for\n");
            return;
        }

        context.StandardError.Write($"libcurl supplied a file descriptor to wait for (maxFd={maxSocket}).  Waiting now ...\n");
        steps.Result = LibTestErrorCodes.Failure;
    }
}
