using Libcurl.Global;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib558.c</c>: <c>curl_easy_escape</c> with no handle of fourteen bytes, the result freed and nothing
/// printed. Upstream's <c>curlx_malloc(558)</c> and its free, which only the memory-debug log sees, have no
/// managed counterpart. No transfer; the URL is unused.
/// </summary>
public sealed class Lib558 : ILibTest
{
    /// <summary>The bytes the program escapes.</summary>
    public static ReadOnlyMemory<byte> Bytes { get; } = new byte[]
    {
        0x2f, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x91, 0xa2, 0xb3, 0xc4, 0xd5, 0xe6, 0xf7,
    };

    /// <inheritdoc/>
    public string ProgramName => "lib558";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (_, _) => Escape());

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <returns>A completed task; the escaped text is dropped.</returns>
    public static ValueTask Escape()
    {
        _ = CurlStrings.Escape(Bytes.Span);
        return ValueTask.CompletedTask;
    }
}
