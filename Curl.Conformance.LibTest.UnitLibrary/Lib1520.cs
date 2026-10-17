using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1520.c</c>: a verbose SMTP upload from <c>&lt;sender@example.com&gt;</c> to <c>&lt;recipient@example.com&gt;</c>
/// of <see cref="PayloadLines"/>, one line a read callback call (<see cref="LineReader"/>), whose lone dots the
/// transfer must escape; the transfer's code is the program's. A failing <c>curl_global_init</c> or
/// <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib1520 : ILibTest
{
    /// <summary><c>payload_text</c>: the message's lines.</summary>
    public static readonly IReadOnlyList<string> PayloadLines =
    [
        "From: different\r\n",
        "To: another\r\n",
        "\r\n",
        "\r\n",
        ".\r\n",
        ".\r\n",
        "\r\n",
        ".\r\n",
        "\r\n",
        "body",
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1520";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            CurlSlist recipients = CurlSlist.Append(null, "<recipient@example.com>");
            var reader = new LineReader();
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Upload, 1L),
                    (CurlOption.Readfunction, (ReadCallback)reader.Read),
                    (CurlOption.Readdata, reader),
                    (CurlOption.MailFrom, "<sender@example.com>"),
                    (CurlOption.MailRcpt, recipients),
                    (CurlOption.Verbose, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(recipients);
        }
    }

    /// <summary><c>t1520_read_cb</c>: gives the next of <see cref="PayloadLines"/> each call, then 0.</summary>
    public sealed class LineReader
    {
        private int linesRead;

        /// <summary>Copies the next line.</summary>
        /// <param name="buffer">Where to copy it.</param>
        /// <returns>The line's length; 0 once every line is read or for an empty buffer.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || linesRead == PayloadLines.Count)
            {
                return 0;
            }

            return Encoding.ASCII.GetBytes(PayloadLines[linesRead++], buffer);
        }
    }
}
