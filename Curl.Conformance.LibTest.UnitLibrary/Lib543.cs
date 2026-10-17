using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib543.c</c>: <c>curl_easy_escape</c> of twenty binary bytes, of an empty string and of the first three
/// bytes of <c> 123</c>, each printed to stdout. No transfer; the URL is unused.
/// </summary>
public sealed class Lib543 : ILibTest
{
    /// <summary>The binary bytes the program escapes first.</summary>
    public static ReadOnlyMemory<byte> BinaryBytes { get; } = new byte[]
    {
        0x9c, 0x26, 0x4b, 0x3d, 0x49, 0x4, 0xa1, 0x1,
        0xe0, 0xd8, 0x7c, 0x20, 0xb7, 0xef, 0x53, 0x29, 0xfa,
        0x1d, 0x57, 0xe1,
    };

    /// <inheritdoc/>
    public string ProgramName => "lib543";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the writes.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            StreamWriter output = LibTestRun.StandardOutputWriter(context);
            await using (output.ConfigureAwait(false))
            {
                string text = $"{CurlStrings.Escape(BinaryBytes.Span)}\n"
                    + $"IN: '' OUT: '{CurlStrings.Escape(easy, string.Empty, 0)}'\n"
                    + $"IN: ' 12' OUT: '{CurlStrings.Escape(easy, " 123", 3)}'\n";
                await output.WriteAsync(text.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
