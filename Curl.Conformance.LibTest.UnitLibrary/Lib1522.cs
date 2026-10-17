using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1522.c</c>: a traced POST of <see cref="PostSize"/> bytes of <c>A</c> with no <c>Expect:</c> header and a
/// socket option callback, then <c>uploadSize = N</c> (<c>CURLINFO_SIZE_UPLOAD_T</c>) on stdout and
/// <c>!!!!!!!!!! PASS</c> when it is every byte posted, else <c>sent 40960, libcurl says N</c>; a failing transfer writes
/// <c>curl_easy_perform() failed. e = N</c> on stdout instead. The transfer's code is the program's. Its three
/// <c>easy_setopt</c> checks (ending with <see cref="LibTestErrorCodes.MajorBad"/>) are left out: Libcurl takes these
/// values without fail. Upstream's callback shrinks the socket's
/// send buffer to 4 KB; this one leaves the socket alone, as upstream's does where <c>SO_SNDBUF</c> is missing,
/// since the buffer's size changes no byte the case checks.
/// </summary>
/// <param name="timeProvider">Where the trace's times come from.</param>
public sealed class Lib1522(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The bytes the program posts: 40 KB.</summary>
    public const int PostSize = 40 * 1024;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1522";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider, cancellationToken));

    /// <summary>The program's body.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">Where the trace's times come from.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            byte[] data = new byte[PostSize];
            Array.Fill(data, (byte)'A');
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Sockoptfunction, (SocketOptionCallback)((_, _) => SocketOptionResult.Ok));
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Postfields, data);
            _ = easy.SetOption(CurlOption.Postfieldsize, (long)PostSize);
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
            _ = easy.SetOption(CurlOption.Debugdata, trace);
            _ = easy.SetOption(CurlOption.Debugfunction, (DebugCallback)trace.Trace);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            CurlSlist headers = CurlSlist.Append(null, "Expect:");
            _ = easy.SetOption(CurlOption.Httpheader, headers);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            WriteOutcome(context, easy, steps.Result);
            CurlSlist.FreeAll(headers);
        }
    }

    /// <summary>Writes the upload size and verdict for a finished transfer, or its failure.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="code">The transfer's code.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void WriteOutcome(LibTestContext context, CurlEasy easy, CurlCode code)
    {
        ArgumentNullException.ThrowIfNull(easy);
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        if (code != CurlCode.Ok)
        {
            output.Write($"curl_easy_perform() failed. e = {(int)code}\n");
            return;
        }

        _ = easy.GetInfo(CurlInfo.SizeUploadT, out object? size);
        long uploadSize = Convert.ToInt64(size, CultureInfo.InvariantCulture);
        output.Write($"uploadSize = {uploadSize}\n");
        output.Write(uploadSize == PostSize ? "!!!!!!!!!! PASS\n" : $"sent {PostSize}, libcurl says {uploadSize}\n");
    }
}
