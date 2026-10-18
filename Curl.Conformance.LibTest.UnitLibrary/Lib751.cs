using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib751.c</c>: adds 1000 verbose handles for <c>https://www.example.com/</c> to one multi handle without
/// transferring anything, then, at cleanup, adds each handle again and cleans it up while it is still in the multi
/// handle. A refused add writes <c>MULTI ERROR: &lt;text&gt;</c> and ends the program with <c>CURLE_FAILED_INIT</c>;
/// any failure also writes <c>ERROR: &lt;text&gt;</c>.
/// </summary>
public sealed class Lib751 : ILibTest
{
    /// <summary>How many handles upstream adds.</summary>
    public const int HandleCount = 1000;

    /// <inheritdoc/>
    public string ProgramName => "lib751";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, HandleCount));

    /// <summary>The body after <c>curl_global_init</c>: the multi handle, the adds, then the cleanup.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="handleCount">How many handles to add, <see cref="HandleCount"/> in the program.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, int handleCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        CurlMulti? multi = library.IsCleanedUp ? null : library.MultiInit();
        if (multi is null)
        {
            steps.Result = CurlCode.OutOfMemory;
            WriteError(context, steps);
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            var handles = new List<CurlEasy>();
            steps.Result = AddHandles(context, library, multi, handles, handleCount);
            WriteError(context, steps);
            foreach (CurlEasy easy in handles)
            {
                _ = multi.AddHandle(easy);
                await easy.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // The loop: each handle made, set and added, kept in handles for the cleanup.
    private static CurlCode AddHandles(LibTestContext context, CurlLibrary library, CurlMulti multi, List<CurlEasy> handles, int handleCount)
    {
        for (int i = 0; i < handleCount; i++)
        {
            CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
            if (easy is null)
            {
                return CurlCode.OutOfMemory;
            }

            handles.Add(easy);
            _ = easy.SetOption(CurlOption.Url, "https://www.example.com/");
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            CurlMultiCode code = multi.AddHandle(easy);
            if (code != CurlMultiCode.Ok)
            {
                context.StandardError.Write($"MULTI ERROR: {CurlErrorText.Multi(code)}\n");
                return CurlCode.FailedInit;
            }
        }

        return CurlCode.Ok;
    }

    private static void WriteError(LibTestContext context, LibTestSteps steps)
    {
        if (steps.Result != CurlCode.Ok)
        {
            context.StandardError.Write($"ERROR: {CurlErrorText.Easy(steps.Result)}\n");
        }
    }
}
