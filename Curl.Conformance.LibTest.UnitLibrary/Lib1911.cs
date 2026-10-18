using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1911.c</c>: every string option in the options table but the four <see cref="IsExempt"/> names is set to a
/// string of <see cref="MaxInputLength"/> + 1 <c>A</c>s, which <c>curl_easy_setopt</c> must refuse
/// (<see cref="IsExpectedAnswer"/>). The program first writes <c>string length: &lt;length&gt;</c> to stdout and, for
/// every other answer, <c>curl_easy_setopt(&lt;name&gt;...) returned &lt;code&gt;</c> to stderr; it ends with
/// <c>CURLE_OK</c> when there was none and <see cref="LibTestErrorCodes.Failure"/> otherwise, and with
/// <see cref="LibTestErrorCodes.EasyInit"/> when no easy handle can be created.
/// </summary>
public sealed class Lib1911 : ILibTest
{
    /// <summary>The program's own copy of libcurl's internal <c>CURL_MAX_INPUT_LENGTH</c>.</summary>
    public const int MaxInputLength = 8_000_000;

    /// <inheritdoc/>
    public string ProgramName => "lib1911";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            steps.Result = LibTestErrorCodes.EasyInit;
            return;
        }

        CurlEasy easy = library.EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            string tooLong = new('A', MaxInputLength + 1);
            using (StreamWriter output = LibTestRun.StandardOutputWriter(context))
            {
                output.Write($"string length: {tooLong.Length}\n");
            }

            int errors = CountUnexpectedAnswers(CurlOptions.All, option => easy.SetOption(option, tooLong), context.StandardError);
            steps.Result = ResultFor(errors);
        }
    }

    /// <summary>The program's code for <paramref name="unexpectedAnswers"/> answers that were not refusals.</summary>
    /// <param name="unexpectedAnswers">How many answers were not refusals.</param>
    /// <returns><c>CURLE_OK</c> for none, <see cref="LibTestErrorCodes.Failure"/> otherwise.</returns>
    public static CurlCode ResultFor(int unexpectedAnswers) => unexpectedAnswers == 0 ? CurlCode.Ok : LibTestErrorCodes.Failure;

    /// <summary>Sets each string option of <paramref name="rows"/> but the exempt ones and counts the answers that are not refusals.</summary>
    /// <param name="rows">The options table's rows.</param>
    /// <param name="setOption">Sets an option to the too-long string, answering <c>curl_easy_setopt</c>'s code.</param>
    /// <param name="error">The program's stderr, where each unexpected answer is written.</param>
    /// <returns>How many answers were not refusals.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int CountUnexpectedAnswers(IEnumerable<CurlEasyOption> rows, Func<CurlOption, CurlCode> setOption, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(setOption);
        ArgumentNullException.ThrowIfNull(error);
        int errors = 0;
        foreach (CurlEasyOption row in rows.Where(row => row.Type == CurlOptionType.String && !IsExempt(row.Id)))
        {
            CurlCode result = setOption(row.Id);
            if (!IsExpectedAnswer(result))
            {
                error.Write($"curl_easy_setopt({row.Name}...) returned {(int)result}\n");
                errors++;
            }
        }

        return errors;
    }

    /// <summary>Whether the program leaves <paramref name="option"/> out: the string options that are safe for abuse.</summary>
    /// <param name="option">The option.</param>
    /// <returns><see langword="true"/> for <c>CURLOPT_PROXY_TLSAUTH_TYPE</c>, <c>CURLOPT_TLSAUTH_TYPE</c>, <c>CURLOPT_RANDOM_FILE</c> and <c>CURLOPT_EGDSOCKET</c>.</returns>
    public static bool IsExempt(CurlOption option) =>
        option is CurlOption.ProxyTlsauthType or CurlOption.TlsauthType or CurlOption.RandomFile or CurlOption.Egdsocket;

    /// <summary>Whether <paramref name="result"/> is one of the refusals the program accepts for a too-long string.</summary>
    /// <param name="result">The <c>curl_easy_setopt</c> answer.</param>
    /// <returns><see langword="true"/> for a bad argument, an unknown option, one not built in, or an unsupported protocol.</returns>
    public static bool IsExpectedAnswer(CurlCode result) =>
        result is CurlCode.BadFunctionArgument or CurlCode.UnknownOption or CurlCode.NotBuiltIn or CurlCode.UnsupportedProtocol;
}
