using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib555.c</c>: <c>lib547.c</c>'s verbose POST with headers on, read once from a callback and rewound by a
/// <c>CURLOPT_IOCTLFUNCTION</c> (<see cref="Lib547.BlurbReader"/>), through the proxy <c>libtest_arg2</c> as
/// <c>libtest_arg3</c> with Basic, Digest or NTLM, run through a multi handle's <c>select()</c> loop of 100 ms
/// (cleanup type PA).
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c>.</param>
public sealed class Lib555(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib555";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reader = new Lib547.BlurbReader(context.StandardError);
        return LibTestRun.SingleMultiTransferAsync(
            steps,
            library,
            clock,
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Ioctlfunction, (IoctlCallback)reader.Ioctl),
                (CurlOption.Ioctldata, reader),
                (CurlOption.Readfunction, (ReadCallback)reader.Read),
                (CurlOption.Readdata, reader),
                (CurlOption.Postfieldsize, (long)Lib547.UploadThis.Length),
                (CurlOption.Post, 1L),
                (CurlOption.Proxy, context.Argument2),
                (CurlOption.Proxyuserpwd, context.Argument3),
                (CurlOption.Proxyauth, (long)(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Ntlm)),
            ],
            100,
            cancellationToken);
    }
}
