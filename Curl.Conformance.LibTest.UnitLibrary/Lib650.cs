using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib650.c</c>: every <c>curl_formadd</c> feature no other program uses - a copied field with content headers,
/// a pointer field named by length from a <c>CURLFORM_ARRAY</c> whose data changes after the call, three files of
/// <c>libtest_arg2</c> with a content type, the file's contents, the form's length so far from
/// <c>curl_formget</c>, and stdin - posted verbose with headers, following a 301 as a POST. A failing
/// <c>curl_formadd</c> is printed to stdout and ends the program with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
#pragma warning disable CS0618 // curl_formadd is deprecated upstream too; lib650 exercises it.
public sealed class Lib650 : ILibTest
{
    private const string TestName = "fieldname";
    private const string TestData = "this is what we post to the silly web server";

    /// <inheritdoc/>
    public string ProgramName => "lib650";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: builds the form, then posts it.</summary>
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
        CurlForm? form = null;
        try
        {
            if (BuildForm(library, ref form, context.StandardOutput, context.Argument2) && steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
            {
                await using (easy.ConfigureAwait(false))
                {
                    _ = await steps.SetOptionsAndPerformAsync(
                        easy,
                        [
                            (CurlOption.Writedata, context.StandardOutput),
                            (CurlOption.Url, context.Url),
                            (CurlOption.Httppost, form),
                            (CurlOption.Verbose, 1L),
                            (CurlOption.Followlocation, 1L),
                            (CurlOption.Postredir, (long)PostRedirect.Post301),
                            (CurlOption.Header, 1L),
                        ],
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                steps.Result = LibTestErrorCodes.MajorBad;
            }
        }
        finally
        {
            CurlForm.Free(form);
        }
    }

    /// <summary>The six <c>curl_formadd</c> calls, the fifth carrying the length <c>curl_formget</c> counts first.</summary>
    /// <param name="library">The initialized library.</param>
    /// <param name="form">The form being built.</param>
    /// <param name="output">The program's stdout, where a failing call is printed.</param>
    /// <param name="file">The file the file fields name, <c>libtest_arg2</c>.</param>
    /// <returns>Whether every call succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static bool BuildForm(CurlLibrary library, ref CurlForm? form, Stream output, string? file)
    {
        ArgumentNullException.ThrowIfNull(output);
        CurlSlist headers = CurlSlist.Append(CurlSlist.Append(CurlSlist.Append(null, "X-customheader-1: Header 1 data"), "X-customheader-2: Header 2 data"), "Content-Type: text/plain");
        byte[] pointed = Encoding.ASCII.GetBytes(TestData);
        CurlFormArgument[] array =
        [
            new(CurlFormOption.PointerContents, pointed),
            new(CurlFormOption.ContentsLength, (long)TestData.Length - 1),
            new(CurlFormOption.End, null),
        ];
        if (!TryAdd(library, ref form, output, 1, [new(CurlFormOption.CopyName, TestName), new(CurlFormOption.CopyContents, TestData), new(CurlFormOption.ContentHeader, headers)])
            || !TryAdd(library, ref form, output, 2, [new(CurlFormOption.PointerName, TestName), new(CurlFormOption.NameLength, (long)TestName.Length - 1), new(CurlFormOption.Array, array), new(CurlFormOption.FileName, "remotefile.txt")]))
        {
            return false;
        }

        pointed[0]++;
        if (!TryAdd(library, ref form, output, 3, [new(CurlFormOption.CopyName, "multifile"), new(CurlFormOption.File, file), new(CurlFormOption.File, file), new(CurlFormOption.ContentType, "text/whatever"), new(CurlFormOption.File, file)])
            || !TryAdd(library, ref form, output, 4, [new(CurlFormOption.CopyName, "filecontents"), new(CurlFormOption.FileContent, file)]))
        {
            return false;
        }

        long length = 0;
        _ = form!.Get(buffer =>
        {
            length += buffer.Length;
            return buffer.Length;
        });
        return TryAdd(library, ref form, output, 5, [new(CurlFormOption.CopyName, "formlength"), new(CurlFormOption.CopyContents, length.ToString(CultureInfo.InvariantCulture))])
            && TryAdd(library, ref form, output, 6, [new(CurlFormOption.CopyName, "standardinput"), new(CurlFormOption.File, "-")]);
    }

    // One curl_formadd; a failing one printed as curl_formadd(N) = <code>.
    private static bool TryAdd(CurlLibrary library, ref CurlForm? form, Stream output, int number, CurlFormArgument[] arguments)
    {
        CurlFormAddCode code = CurlForm.Add(library, ref form, arguments);
        if (code == CurlFormAddCode.Ok)
        {
            return true;
        }

        output.Write(Encoding.ASCII.GetBytes($"curl_formadd({number}) = {(int)code}\n"));
        return false;
    }
}
