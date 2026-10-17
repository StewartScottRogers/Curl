using System.Text;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib678.c</c>: with the URL <c>check</c>, sets <c>CURLOPT_CAINFO_BLOB</c> to <c>silly</c> and prints
/// <c>CURLOPT_CAINFO_BLOB is not supported</c> on stdout when it is refused, ending with that code; otherwise a
/// verbose transfer with headers on, the user agent <c>CURLOPT_CAINFO_BLOB</c> and
/// <c>CURLSSLOPT_REVOKE_BEST_EFFORT</c>, trusting the certificates read from the file <c>libtest_arg2</c> as a blob.
/// A file that cannot be read skips the transfer and ends with <see cref="CurlCode.OutOfMemory"/>, as upstream's
/// does. <c>curl_global_init</c>'s answer is not checked, as upstream's is not.
/// </summary>
public sealed class Lib678 : ILibTest
{
    /// <summary><c>CURLSSLOPT_REVOKE_BEST_EFFORT</c>.</summary>
    public const long SslOptionRevokeBestEffort = 1L << 3;

    /// <inheritdoc/>
    public string ProgramName => "lib678";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the support check or the transfer.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            // curl_easy_init gave no handle: test_cert_blob's own failure, or nothing for the check.
            steps.Result = context.Url == "check" ? CurlCode.Ok : CurlCode.FailedInit;
            context.StandardError.Write(context.Url == "check" ? string.Empty : "curl_easy_init() failed\n");
            return;
        }

        await using CurlEasy easy = library.EasyInit();
        if (context.Url == "check")
        {
            steps.Result = easy.SetOption(CurlOption.CainfoBlob, "silly"u8.ToArray());
            ReportSupport(steps.Result, context.StandardOutput);
            return;
        }

        byte[]? certificates = LoadFile(context.Argument2);
        steps.Result = CurlCode.OutOfMemory;
        if (certificates is null)
        {
            return;
        }

        _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
        _ = easy.SetOption(CurlOption.Verbose, 1L);
        _ = easy.SetOption(CurlOption.Header, 1L);
        _ = easy.SetOption(CurlOption.Url, context.Url);
        _ = easy.SetOption(CurlOption.Useragent, "CURLOPT_CAINFO_BLOB");
        _ = easy.SetOption(CurlOption.SslOptions, SslOptionRevokeBestEffort);
        _ = easy.SetOption(CurlOption.CainfoBlob, certificates);
        _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The check's answer: a refused <c>CURLOPT_CAINFO_BLOB</c> prints <c>CURLOPT_CAINFO_BLOB is not supported</c>.</summary>
    /// <param name="code">What setting the option answered.</param>
    /// <param name="output">The program's stdout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static void ReportSupport(CurlCode code, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (code != CurlCode.Ok)
        {
            output.Write(Encoding.ASCII.GetBytes("CURLOPT_CAINFO_BLOB is not supported\n"));
        }
    }

    /// <summary><c>loadfile</c>: the whole file's bytes.</summary>
    /// <param name="path">The file, or <see langword="null"/>.</param>
    /// <returns>The bytes, or <see langword="null"/> when there is no path or no such file.</returns>
    public static byte[]? LoadFile(string? path) => path is not null && File.Exists(path) ? File.ReadAllBytes(path) : null;
}
