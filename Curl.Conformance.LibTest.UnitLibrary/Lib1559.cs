using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1559.c</c>: a string of <see cref="Excessive"/> - 1 <c>a</c>s set as <c>CURLOPT_URL</c> and
/// <c>CURLOPT_POSTFIELDS</c>, then as a URL handle's URL, scheme (with <c>CURLU_NON_SUPPORT_SCHEME</c>) and user,
/// each answer written on stdout (case 1559 expects 43, <c>CURLE_BAD_FUNCTION_ARGUMENT</c>, for the URL, 0 for the
/// post data and 3, <c>CURLUE_MALFORMED_INPUT</c>, for each URL part). The program ends with the
/// <c>CURLOPT_POSTFIELDS</c> answer.
/// </summary>
public sealed class Lib1559 : ILibTest
{
    /// <summary><c>EXCESSIVE</c>: the size of the buffer, its last byte the string's terminator.</summary>
    public const int Excessive = 10 * 1000 * 1000;

    /// <inheritdoc/>
    public string ProgramName => "lib1559";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            string longUrl = new('a', Excessive - 1);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            CurlCode result = easy.SetOption(CurlOption.Url, longUrl);
            output.Write($"CURLOPT_URL {Excessive} bytes URL == {(int)result}\n");
            steps.Result = easy.SetOption(CurlOption.Postfields, longUrl);
            output.Write($"CURLOPT_POSTFIELDS {Excessive} bytes data == {(int)steps.Result}\n");

            using CurlUrlHandle url = library.UrlInit();
            WritePart(output, url, CurlUrlPart.Url, CurlUrlFlags.None, longUrl, "CURLUPART_URL", "URL");
            WritePart(output, url, CurlUrlPart.Scheme, CurlUrlFlags.NonSupportScheme, longUrl, "CURLUPART_SCHEME", "scheme");
            WritePart(output, url, CurlUrlPart.User, CurlUrlFlags.None, longUrl, "CURLUPART_USER", "user");
        }
    }

    private static void WritePart(StreamWriter output, CurlUrlHandle url, CurlUrlPart part, CurlUrlFlags flags, string value, string partName, string what)
    {
        CurlUrlCode code = url.Set(part, value, flags);
        output.Write($"{partName} {Excessive} bytes {what} == {(int)code} ({CurlUrlHandle.ErrorText(code)})\n");
    }
}
