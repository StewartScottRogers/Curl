using System.Runtime.CompilerServices;
using System.Text;
using Libcurl.Global;

namespace Curl.Conformance.LibTest;

/// <summary>
/// What every ported program does around its body: <c>curl_global_init(CURL_GLOBAL_ALL)</c> over the
/// context's engine, the body on the library, then <c>curl_global_cleanup</c>, answering the program's
/// <c>result</c>. A port keeps its body in a public method taking the library, so a test can hand it a
/// library already cleaned up and reach the failing <c>curl_easy_init</c> and <c>curl_multi_init</c> checks.
/// </summary>
public static class LibTestRun
{
    /// <summary>
    /// Runs a program that checks <c>curl_global_init</c> by hand (<c>curl_global_init() failed</c>,
    /// <see cref="LibTestErrorCodes.MajorBad"/>; <see cref="LibTestSteps.GlobalInitOrMajorBad"/>).
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="test">The program's body after the init.</param>
    /// <returns>The exit code the program returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="test"/> is <see langword="null"/>.</exception>
    public static async Task<LibTestResult> WithLibraryAsync(LibTestContext context, Func<LibTestSteps, CurlLibrary, ValueTask> test)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(test);
        var steps = new LibTestSteps(context.StandardError);
        if (steps.GlobalInitOrMajorBad(context.Engine, CurlGlobalFlags.All, out CurlLibrary? library))
        {
            await using (library.ConfigureAwait(false))
            {
                await test(steps, library).ConfigureAwait(false);
            }
        }

        return steps.ToLibTestResult();
    }

    /// <summary>
    /// Runs a program that opens with the <c>global_init</c> macro (<see cref="LibTestSteps.GlobalInit"/>,
    /// written with the caller's file and line).
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="test">The program's body after the init.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>The exit code the program returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="test"/> is <see langword="null"/>.</exception>
    public static async Task<LibTestResult> WithMacroLibraryAsync(
        LibTestContext context,
        Func<LibTestSteps, CurlLibrary, ValueTask> test,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(test);
        var steps = new LibTestSteps(context.StandardError);
        if (steps.GlobalInit(context.Engine, CurlGlobalFlags.All, out CurlLibrary? library, file, line))
        {
            await using (library.ConfigureAwait(false))
            {
                await test(steps, library).ConfigureAwait(false);
            }
        }

        return steps.ToLibTestResult();
    }

    /// <summary>
    /// The body <c>lib502.c</c> and <c>lib503.c</c> share: <c>easy_init</c>, <c>multi_init</c>, the options,
    /// <c>multi_add_handle</c>, the <c>select()</c> loop (<see cref="LibTestSteps.PerformUntilDoneAsync"/>), then
    /// cleanup type PA: <c>curl_multi_remove_handle</c>, <c>curl_multi_cleanup</c>, <c>curl_easy_cleanup</c>.
    /// </summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="options">The easy options, in the program's order.</param>
    /// <param name="selectTimeoutMilliseconds">The <c>select()</c> timeout of each turn.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <param name="afterTransfer">What the program does with the handle once the loop ended with no step failing, before the cleanup; <see langword="null"/> for nothing.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask SingleMultiTransferAsync(
        LibTestSteps steps,
        CurlLibrary library,
        LibTestClock clock,
        IEnumerable<(Libcurl.Options.CurlOption Option, object? Value)> options,
        int selectTimeoutMilliseconds,
        CancellationToken cancellationToken,
        Action<Libcurl.Easy.CurlEasy>? afterTransfer = null)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out Libcurl.Easy.CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (!steps.MultiInit(library, out Libcurl.Multi.CurlMulti? multi))
            {
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                if (steps.SetOptionsAndAddHandle(easy, options, multi)
                    && await steps.PerformUntilDoneAsync(multi, clock, selectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false))
                {
                    afterTransfer?.Invoke(easy);
                }

                multi.RemoveHandle(easy);
            }
        }
    }

    /// <summary>
    /// A writer over the program's <c>stdout</c> for <c>curl_mprintf</c>: UTF-8 with no byte order mark,
    /// flushed on every write, leaving the stream open.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <returns>The writer; the program disposes it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static StreamWriter StandardOutputWriter(LibTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new StreamWriter(context.StandardOutput, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
    }
}
