using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib643.c</c> (cases 643 and 645): a verbose <c>CURLOPT_MIMEPOST</c> with headers on, twice - first with the
/// old-style part <c>sendfile</c>, then the new-style <c>sendfile alternative</c> - each beside a second callback
/// part, two data parts and a file-named buffer part, the callbacks giving <c>dummy\n</c> one byte a call. Case
/// 643 gives the callback parts their size; case 645 leaves it unknown (-1), so they go chunked. A failing part
/// call is printed to stdout. After both posts it checks that <c>curl_mime_subparts</c> refuses a cycle with
/// <c>CURLE_BAD_FUNCTION_ARGUMENT</c>, else <see cref="LibTestErrorCodes.Failure"/>.
/// </summary>
public sealed class Lib643 : ILibTest
{
    /// <summary>The bytes each callback part gives.</summary>
    public const string TestData = "dummy\n";

    /// <inheritdoc/>
    public string ProgramName => "lib643";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the old-style post, the new-style one, then the cycle check.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (await TestOnceAsync(context, steps, library, oldStyle: true, cancellationToken).ConfigureAwait(false)
            && await TestOnceAsync(context, steps, library, oldStyle: false, cancellationToken).ConfigureAwait(false))
        {
            steps.Result = CyclicAdd(library);
        }
    }

    /// <summary><c>t643_test_once</c>: builds the MIME structure, posts it and frees it.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="oldStyle">Whether the first part is the old-style one.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the post succeeded.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> TestOnceAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, bool oldStyle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return false;
        }

        await using (easy.ConfigureAwait(false))
        {
            using CurlMime mime = library.MimeInit();
            BuildParts(mime, context.StandardOutput, context.TestNumber == 643 ? TestData.Length : -1, oldStyle);
            return await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Mimepost, mime),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The five parts, each part's failing call printed as <c>curl_mime_xxx(N) = &lt;error&gt;</c>.</summary>
    /// <param name="mime">The structure to add the parts to.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="size">The callback parts' size, or -1 for unknown.</param>
    /// <param name="oldStyle">Whether the first part is the old-style one.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void BuildParts(CurlMime mime, Stream output, long size, bool oldStyle)
    {
        ArgumentNullException.ThrowIfNull(mime);
        ArgumentNullException.ThrowIfNull(output);
        CurlMimePart first = mime.AddPart();
        CurlMimePart second = mime.AddPart();
        CurlMimePart third = mime.AddPart();
        CurlMimePart fourth = mime.AddPart();
        CurlMimePart fifth = mime.AddPart();
        Func<CurlCode>[][] parts =
        [
            [
                () => first.SetName(oldStyle ? "sendfile" : "sendfile alternative"),
                () => first.SetDataCallback(size, new ByteReader().Read, null),
                () => first.SetFileName(oldStyle ? "postit2.c" : "filename 2 "),
            ],
            [() => second.SetName("callbackdata"), () => second.SetDataCallback(size, new ByteReader().Read, null)],
            [() => third.SetName("filename"), () => third.SetData("postit2.c")],
            [() => fourth.SetName("submit"), () => fourth.SetData("send")],
            [() => fifth.SetName("somename"), () => fifth.SetFileName("somefile.txt"), () => fifth.SetData("blah blah")],
        ];
        for (int i = 0; i < parts.Length; i++)
        {
            CurlCode code = LibTestMimeCalls.FirstFailure(parts[i], out _);
            if (code != CurlCode.Ok)
            {
                output.Write(Encoding.ASCII.GetBytes($"curl_mime_xxx({i + 1}) = {CurlErrorText.Easy(code)}\n"));
            }
        }
    }

    /// <summary>
    /// <c>t643_cyclic_add</c>: a part made its own structure's subparts, then, when that is refused, the cycle
    /// through a nested structure, must both be refused with <c>CURLE_BAD_FUNCTION_ARGUMENT</c>.
    /// </summary>
    /// <param name="library">The initialized library.</param>
    /// <returns><see cref="CurlCode.Ok"/> when the cycle was refused, else <see cref="LibTestErrorCodes.Failure"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public static CurlCode CyclicAdd(CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        using CurlMime mime = library.MimeInit();
        CurlMimePart part = mime.AddPart();
        CurlCode code = part.SetSubParts(mime);
        if (code == CurlCode.BadFunctionArgument)
        {
            CurlMime submime = library.MimeInit();
            CurlMimePart subpart = submime.AddPart();
            _ = part.SetSubParts(submime);
            code = subpart.SetSubParts(mime);
        }

        return code == CurlCode.BadFunctionArgument ? CurlCode.Ok : LibTestErrorCodes.Failure;
    }

    /// <summary><c>t643_read_cb</c>: <see cref="TestData"/> one byte a call.</summary>
    public sealed class ByteReader
    {
        private int position;

        /// <summary>Copies the next byte, if any, into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Where the byte goes.</param>
        /// <returns>1, or 0 for an empty buffer or once every byte was given.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || position == TestData.Length)
            {
                return 0;
            }

            buffer[0] = (byte)TestData[position++];
            return 1;
        }
    }
}
