using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1965.c</c>: each of <see cref="Schemes"/> set as a fresh <c>CURLU</c>'s scheme with
/// <c>CURLU_NON_SUPPORT_SCHEME</c>, writing <c>&lt;scheme&gt; ACCEPTED</c> or <c>&lt;scheme&gt; REJECTED</c> on stdout,
/// so the scheme syntax check (a letter, then letters, digits, <c>+</c>, <c>-</c> and <c>.</c>, at most 40 of them) is
/// seen without a transfer. The program always succeeds.
/// </summary>
public sealed class Lib1965 : ILibTest
{
    /// <summary>The schemes the program tries, in its order.</summary>
    public static IReadOnlyList<string> Schemes { get; } =
    [
        "bad!", "bad{", "bad/", "bad\\", "a!",
        "a+123", "http-2", "http.1",
        "a+-.123", "http-+++2", "http.1--",
        "+a123", "-http2", ".http1",
        "ABC2", "2CBA", string.Empty, "a",
        "aaaaaaaaaabbbbbbbbbbccccccccccdddddddddd",
        "aaaaaaaaaabbbbbbbbbbccccccccccdddddddddde",
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1965";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (_, library) => TestAsync(context, library));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(library);
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        foreach (string scheme in Schemes)
        {
            using CurlUrlHandle url = library.UrlInit();
            CurlUrlCode code = url.Set(CurlUrlPart.Scheme, scheme, CurlUrlFlags.NonSupportScheme);
            output.Write($"{scheme} {(code == CurlUrlCode.Ok ? "ACCEPTED" : "REJECTED")}\n");
        }

        return ValueTask.CompletedTask;
    }
}
