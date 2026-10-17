using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1156.c</c>: sixteen verbose transfers, one per combination of range or resume, fail-on-error, a 416 reply and
/// a <c>Content-Range</c> header, each from the URL with its reply number appended (<c>%04u</c>), each checked for the
/// code it must end with and, where the body must be ignored, for no body. A mismatch writes its line on stdout; the
/// program then writes the combined status (<c>0</c> or <c>1</c>) and ends with <see cref="LibTestErrorCodes.Failure"/> when it is 1.
/// </summary>
public sealed class Lib1156 : ILibTest
{
    /// <summary><c>F_RESUME</c>: resume from byte 3 rather than ask for the range 3-1000000.</summary>
    public const int Resume = 1 << 0;

    /// <summary><c>F_HTTP416</c>: the server answers 416.</summary>
    public const int Http416 = 1 << 1;

    /// <summary><c>F_FAIL</c>: <c>CURLOPT_FAILONERROR</c>.</summary>
    public const int Fail = 1 << 2;

    /// <summary><c>F_CONTENTRANGE</c>: the reply carries <c>Content-Range</c>.</summary>
    public const int ContentRange = 1 << 3;

    /// <summary><c>F_IGNOREBODY</c>: no body may reach the write callback.</summary>
    public const int IgnoreBody = 1 << 4;

    /// <summary><c>testparams</c>: each transfer's flags and the code it must end with.</summary>
    public static IReadOnlyList<(int Flags, CurlCode Result)> Parameters { get; } =
    [
        (0, CurlCode.Ok),
        (ContentRange, CurlCode.Ok),
        (Fail, CurlCode.Ok),
        (Fail | ContentRange, CurlCode.Ok),
        (Http416, CurlCode.Ok),
        (Http416 | ContentRange, CurlCode.Ok),
        (Http416 | Fail | IgnoreBody, CurlCode.HttpReturnedError),
        (Http416 | Fail | ContentRange | IgnoreBody, CurlCode.HttpReturnedError),
        (Resume | IgnoreBody, CurlCode.RangeError),
        (Resume | ContentRange, CurlCode.Ok),
        (Resume | Fail | IgnoreBody, CurlCode.RangeError),
        (Resume | Fail | ContentRange, CurlCode.Ok),
        (Resume | Http416 | IgnoreBody, CurlCode.Ok),
        (Resume | Http416 | ContentRange | IgnoreBody, CurlCode.Ok),
        (Resume | Http416 | Fail | IgnoreBody, CurlCode.Ok),
        (Resume | Http416 | Fail | ContentRange | IgnoreBody, CurlCode.Ok),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1156";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, Parameters, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: one handle and one transfer per row, then the status line.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="parameters">The rows, as <see cref="Parameters"/>.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        IEnumerable<(int Flags, CurlCode Result)> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(parameters);
        await using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        int status = 0;
        int number = 0;
        foreach ((int flags, CurlCode expected) in parameters)
        {
            if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                status |= await OneTestAsync(context, easy, output, flags, expected, number++, cancellationToken).ConfigureAwait(false);
            }
        }

        output.Write($"{status}\n");
        steps.Result = status == 0 ? CurlCode.Ok : LibTestErrorCodes.Failure;
    }

    // onetest: 0 when the transfer ended as expected, else 1 after writing why.
    private static async ValueTask<int> OneTestAsync(LibTestContext context, CurlEasy easy, StreamWriter output, int flags, CurlCode expected, int number, CancellationToken cancellationToken)
    {
        bool hasBody = false;
        _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data =>
        {
            hasBody |= !data.IsEmpty;
            return data.Length;
        }));
        SetOptions(context, easy, flags);
        CurlCode result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (result != expected)
        {
            output.Write($"{number}: bad error code ({(int)result}): {Describe(flags)}, expected={(int)expected}\n");
            return 1;
        }

        if (hasBody && Has(flags, IgnoreBody))
        {
            output.Write($"body should be ignored and is not: {Describe(flags)}\n");
            return 1;
        }

        return 0;
    }

    // The row's URL (reply 0 to 3), resume or range, and fail-on-error.
    private static void SetOptions(LibTestContext context, CurlEasy easy, int flags)
    {
        int replySelector = (Has(flags, ContentRange) ? 1 : 0) + (Has(flags, Http416) ? 2 : 0);
        bool resume = Has(flags, Resume);
        _ = easy.SetOption(CurlOption.Url, $"{context.Url}{replySelector:D4}");
        _ = easy.SetOption(CurlOption.Verbose, 1L);
        _ = easy.SetOption(CurlOption.ResumeFrom, resume ? 3L : 0L);
        _ = easy.SetOption(CurlOption.Range, resume ? null : "3-1000000");
        _ = easy.SetOption(CurlOption.Failonerror, Has(flags, Fail) ? 1L : 0L);
    }

    private static bool Has(int flags, int flag) => (flags & flag) != 0;

    private static string Describe(int flags) =>
        $"resume={YesNo(flags, Resume)}, fail={YesNo(flags, Fail)}, http416={YesNo(flags, Http416)}, content-range={YesNo(flags, ContentRange)}";

    private static string YesNo(int flags, int flag) => Has(flags, flag) ? "yes" : "no";
}
