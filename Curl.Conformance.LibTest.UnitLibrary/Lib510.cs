using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib510.c</c>: a chunked POST (<c>Transfer-Encoding: chunked</c> in <c>CURLOPT_HTTPHEADER</c>) of four
/// strings handed over one per read callback, verbose and headers on; case 565 adds Digest authentication
/// as <c>foo:bar</c>.
/// </summary>
public sealed class Lib510 : ILibTest
{
    /// <summary>The strings the read callback hands over, one per call.</summary>
    public static IReadOnlyList<string> PostParts { get; } = ["one", "two", "three", "and a final longer crap: four"];

    /// <inheritdoc/>
    public string ProgramName => "lib510";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
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
            CurlSlist headers = CurlSlist.Append(null, "Transfer-Encoding: chunked");
            var reader = new PartReader(PostParts, context.StandardError);
            List<(CurlOption Option, object? Value)> options =
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Post, 1L),
                (CurlOption.Readfunction, (ReadCallback)reader.Read),
                (CurlOption.Readdata, reader),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Httpheader, headers),
            ];
            if (context.TestNumber == 565)
            {
                options.Add((CurlOption.Httpauth, (long)HttpAuthenticationMethods.Digest));
                options.Add((CurlOption.Userpwd, "foo:bar"));
            }

            await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(headers);
        }
    }

    /// <summary><c>t510_read_cb</c>: hands over one string a call, then 0.</summary>
    /// <param name="parts">The strings to hand over.</param>
    /// <param name="standardError">Where a too-small buffer is reported.</param>
    public sealed class PartReader(IReadOnlyList<string> parts, TextWriter standardError)
    {
        private int counter;

        /// <summary>Copies the next string into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Where the string goes.</param>
        /// <returns>The string's length, or 0 when none is left, the buffer is empty or it is too small.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || counter == parts.Count)
            {
                return 0;
            }

            byte[] data = Encoding.ASCII.GetBytes(parts[counter]);
            if (buffer.Length < data.Length)
            {
                standardError.Write("read buffer is too small to run test\n");
                return 0;
            }

            data.CopyTo(buffer);
            counter++;
            return data.Length;
        }
    }
}
