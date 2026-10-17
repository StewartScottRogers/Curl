using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib554.c</c>: a verbose <c>CURLOPT_HTTPPOST</c> with headers on, twice - first with the old-style
/// <c>CURLFORM_CONTENTSLENGTH</c> stream field <c>sendfile</c>, then with the new-style <c>CURLFORM_CONTENTLEN</c>
/// field <c>sendfile alternative</c> - each beside a second stream field, two copied fields and a buffer, the
/// streams read one byte a call (<see cref="StreamReader"/>). Case 587 reads with a callback that aborts. A
/// failing <c>curl_formadd</c> is printed to stdout; the second post runs only when the first succeeded.
/// </summary>
#pragma warning disable CS0618 // curl_formadd is deprecated upstream too; lib554 exercises it.
public sealed class Lib554 : ILibTest
{
    /// <summary>The bytes each stream field gives.</summary>
    public const string TestData = "this is what we post to the silly web server\n";

    /// <inheritdoc/>
    public string ProgramName => "lib554";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>: the old-style post, then the new-style one.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (await TestOnceAsync(context, steps, library, oldStyle: true, cancellationToken).ConfigureAwait(false))
        {
            await TestOnceAsync(context, steps, library, oldStyle: false, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t554_test_once</c>: builds the form, posts it and frees it.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="oldStyle">Whether the first field is the old-style one.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the post succeeded.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> TestOnceAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, bool oldStyle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        var reader = new StreamReader();
        CurlForm? form = BuildForm(library, context.StandardOutput, oldStyle, reader);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            CurlForm.Free(form);
            return false;
        }

        bool succeeded;
        await using (easy.ConfigureAwait(false))
        {
            ReadCallback read = context.TestNumber == 587 ? Abort : reader.Read;
            succeeded = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Post, 1L),
                    (CurlOption.Postfieldsize, (long)TestData.Length),
                    (CurlOption.Readfunction, read),
                    (CurlOption.Httppost, form),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }

        CurlForm.Free(form);
        return succeeded;
    }

    /// <summary>The five <c>curl_formadd</c> calls, each failing one printed as <c>curl_formadd(N) = code</c>.</summary>
    /// <param name="library">The initialized library.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="oldStyle">Whether the first field is the old-style one.</param>
    /// <param name="reader">The streams' reader, which each stream field is added to.</param>
    /// <returns>The form, or <see langword="null"/> when no call succeeded.</returns>
    public static CurlForm? BuildForm(CurlLibrary library, Stream output, bool oldStyle, StreamReader reader)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(reader);
        long size = TestData.Length;
        CurlFormArgument[][] calls =
        [
            oldStyle
                ? [new(CurlFormOption.CopyName, "sendfile"), new(CurlFormOption.Stream, reader.AddStream()), new(CurlFormOption.ContentsLength, size), new(CurlFormOption.FileName, "postit2.c")]
                : [new(CurlFormOption.CopyName, "sendfile alternative"), new(CurlFormOption.Stream, reader.AddStream()), new(CurlFormOption.ContentLength, size), new(CurlFormOption.FileName, "filename 2 ")],
            [new(CurlFormOption.CopyName, "callbackdata"), new(CurlFormOption.Stream, reader.AddStream()), new(CurlFormOption.ContentsLength, size)],
            [new(CurlFormOption.CopyName, "filename"), new(CurlFormOption.CopyContents, "postit2.c")],
            [new(CurlFormOption.CopyName, "submit"), new(CurlFormOption.CopyContents, "send"), new(CurlFormOption.ContentType, "text/plain")],
            [new(CurlFormOption.CopyName, "somename"), new(CurlFormOption.Buffer, "somefile.txt"), new(CurlFormOption.BufferPointer, "blah blah"u8.ToArray()), new(CurlFormOption.BufferLength, 9L)],
        ];
        CurlForm? form = null;
        for (int i = 0; i < calls.Length; i++)
        {
            CurlFormAddCode code = CurlForm.Add(library, ref form, calls[i]);
            if (code != CurlFormAddCode.Ok)
            {
                output.Write(Encoding.ASCII.GetBytes($"curl_formadd({i + 1}) = {(int)code}\n"));
            }
        }

        return form;
    }

    /// <summary><c>t587_read_cb</c>: aborts the transfer.</summary>
    /// <param name="buffer">Unused.</param>
    /// <returns><c>CURL_READFUNC_ABORT</c>.</returns>
    public static long Abort(Span<byte> buffer) => (long)ReadCallbackResult.Abort;

    /// <summary>
    /// <c>t554_read_cb</c> over each <c>t554_WriteThis</c> in turn: one byte a call. A managed read callback gets
    /// no <c>CURLFORM_STREAM</c> pointer, so the streams are read in the order they were added, as the form sends them.
    /// </summary>
    public sealed class StreamReader
    {
        private int streams;
        private int position;

        /// <summary>Adds one stream of <see cref="TestData"/>.</summary>
        /// <returns>The stream's object, which <c>CURLFORM_STREAM</c> carries.</returns>
        public object AddStream()
        {
            streams++;
            return this;
        }

        /// <summary>The next byte of the current stream, moving to the next stream once one is used up.</summary>
        /// <param name="buffer">Where the byte goes.</param>
        /// <returns>1, or 0 when the buffer is empty or every stream is used up.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || position >= streams * TestData.Length)
            {
                return 0;
            }

            buffer[0] = (byte)TestData[position++ % TestData.Length];
            return 1;
        }
    }
}
#pragma warning restore CS0618
