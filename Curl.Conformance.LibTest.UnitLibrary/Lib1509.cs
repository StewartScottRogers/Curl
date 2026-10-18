using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1509.c</c>: a verbose GET with headers through the HTTP proxy <c>libtest_arg2</c> with
/// <c>CURLOPT_HTTPPROXYTUNNEL</c>, its body copied to stdout and its headers counted by a header callback, then
/// <c>header length is ........: N</c> (<c>CURLINFO_HEADER_SIZE</c>) and <c>header length should be..: N</c> (the
/// count) on stdout. A failing transfer writes <c>&lt;file&gt;:&lt;line&gt; curl_easy_perform() failed, with code N
/// (text)</c> on stderr and ends with <see cref="LibTestErrorCodes.MajorBad"/>. The same check of
/// <c>curl_easy_getinfo</c> and the <c>easy_setopt</c> checks are left out: Libcurl answers that info on a live handle,
/// and takes these values, without fail.
/// </summary>
public sealed class Lib1509 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1509";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            long realHeaderSize = 0;
            long WriteHeader(ReadOnlySpan<byte> header)
            {
                realHeaderSize += header.Length;
                return header.Length;
            }

            _ = steps.EasySetOptions(easy, [
                (CurlOption.Proxy, context.Argument2),
                (CurlOption.Writefunction, (WriteCallback)(data => WriteOutput(context, data))),
                (CurlOption.Headerfunction, (HeaderCallback)WriteHeader),
                (CurlOption.Header, 1L),
                (CurlOption.Verbose, 1L),
                (CurlOption.Url, context.Url),
                (CurlOption.Httpproxytunnel, 1L),
            ]);
            var code = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (code != Curl.Api.Codes.CurlCode.Ok)
            {
                context.StandardError.Write($"{Path.GetFileName(file)}:{line} curl_easy_perform() failed, with code {(int)code} ({CurlErrorText.Easy(code)})\n");
                steps.Result = LibTestErrorCodes.MajorBad;
                return;
            }

            easy.GetInfo(CurlInfo.HeaderSize, out object? headerSize);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"header length is ........: {Convert.ToInt64(headerSize, CultureInfo.InvariantCulture)}\n");
            output.Write($"header length should be..: {realHeaderSize}\n");
        }
    }

    /// <summary><c>WriteOutput</c>: copies the bytes to stdout.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="data">The bytes.</param>
    /// <returns>The byte count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static long WriteOutput(LibTestContext context, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.StandardOutput.Write(data);
        return data.Length;
    }
}
