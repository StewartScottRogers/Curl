using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib655.c</c>: a <c>libtest_debug_cb</c>-traced transfer (no hex, with times) whose
/// <c>CURLOPT_RESOLVER_START_FUNCTION</c> refuses, which must end <c>CURLE_ABORTED_BY_CALLBACK</c>, then one of
/// <c>libtest_arg2</c> whose callback allows it, which must succeed; the callback must have run twice. Each broken
/// expectation is written on stderr. Upstream also compares the callback's user data with the string it set,
/// which a managed callback's closure makes needless, so that check is left out.
/// </summary>
/// <param name="timeProvider">Where the trace's times come from.</param>
public sealed class Lib655(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib655";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the refused transfer, then the allowed one.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
            int calls = 0;
            _ = steps.EasySetOptions(easy, OptionsOf(context, context.Url, trace, _ => Count(ref calls, 1)));
            CurlCode refused = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (refused != CurlCode.AbortedByCallback)
            {
                context.StandardError.Write($"curl_easy_perform should have returned CURLE_ABORTED_BY_CALLBACK but instead returned error {(int)refused}\n");
                steps.Result = refused == CurlCode.Ok ? LibTestErrorCodes.Failure : refused;
                return;
            }

            _ = steps.EasySetOptions(easy, OptionsOf(context, context.Argument2, trace, _ => Count(ref calls, 0)));
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                context.StandardError.Write("curl_easy_perform failed.\n");
                return;
            }

            if (calls != 2)
            {
                context.StandardError.Write($"Unexpected number of callbacks: {calls}\n");
                steps.Result = LibTestErrorCodes.Failure;
            }
        }
    }

    // resolver_alloc_cb_fail and _pass: counts the call and answers whether to refuse it.
    private static int Count(ref int calls, int answer)
    {
        calls++;
        return answer;
    }

    // The options both transfers set: the program's stdout, the URL, the trace and the resolver start callback.
    private static (CurlOption Option, object? Value)[] OptionsOf(LibTestContext context, string? url, LibTestDebugTrace trace, ResolverStartCallback resolverStart) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Url, url),
        (CurlOption.Debugdata, trace),
        (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
        (CurlOption.Verbose, 1L),
        (CurlOption.ResolverStartFunction, resolverStart),
    ];
}
