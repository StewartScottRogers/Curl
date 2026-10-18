using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib547.c</c>: a verbose POST with headers on through the proxy <c>libtest_arg2</c> as the proxy user
/// <c>libtest_arg3</c>, Basic, Digest or NTLM. Case 548 sets the body with <c>CURLOPT_POSTFIELDS</c>; the others
/// hand it over once from a read callback with its size set, rewound by a <c>CURLOPT_IOCTLFUNCTION</c>
/// (<see cref="BlurbReader"/>).
/// </summary>
public sealed class Lib547 : ILibTest
{
    /// <summary>The body the program posts.</summary>
    public const string UploadThis = "this is the blurb we want to upload\n";

    /// <inheritdoc/>
    public string ProgramName => "lib547";

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
            List<(CurlOption Option, object? Value)> options =
                [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Verbose, 1L), (CurlOption.Header, 1L)];
            if (context.TestNumber == 548)
            {
                options.Add((CurlOption.Postfields, UploadThis));
            }
            else
            {
                var reader = new BlurbReader(context.StandardError);
                options.Add((CurlOption.Ioctlfunction, (IoctlCallback)reader.Ioctl));
                options.Add((CurlOption.Ioctldata, reader));
                options.Add((CurlOption.Readfunction, (ReadCallback)reader.Read));
                options.Add((CurlOption.Readdata, reader));
                options.Add((CurlOption.Postfieldsize, (long)UploadThis.Length));
            }

            options.Add((CurlOption.Post, 1L));
            options.Add((CurlOption.Proxy, context.Argument2));
            options.Add((CurlOption.Proxyuserpwd, context.Argument3));
            options.Add((CurlOption.Proxyauth, (long)(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Ntlm)));
            await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t547_read_cb</c> and <c>t547_ioctl_callback</c> over their shared counter.</summary>
    /// <param name="standardError">Where each call says what it did.</param>
    public sealed class BlurbReader(TextWriter standardError)
    {
        private int counter;

        /// <summary>Hands over <see cref="UploadThis"/> once until rewound.</summary>
        /// <param name="buffer">Where the body goes.</param>
        /// <returns>The body's length, or 0 when it was already read or the buffer is too small.</returns>
        public long Read(Span<byte> buffer)
        {
            if (counter != 0)
            {
                standardError.Write("READ ALREADY DONE!\n");
                return 0;
            }

            counter++;
            if (buffer.Length >= UploadThis.Length)
            {
                standardError.Write("READ!\n");
                return Encoding.ASCII.GetBytes(UploadThis, buffer);
            }

            standardError.Write("READ NOT FINE!\n");
            return 0;
        }

        /// <summary>On <see cref="IoctlCommand.RestartRead"/>, says <c>REWIND!</c> and clears the counter.</summary>
        /// <param name="command">The command libcurl passes.</param>
        /// <returns><see cref="IoctlResult.Ok"/>, whatever the command.</returns>
        public IoctlResult Ioctl(IoctlCommand command)
        {
            if (command == IoctlCommand.RestartRead)
            {
                standardError.Write("REWIND!\n");
                counter = 0;
            }

            return IoctlResult.Ok;
        }
    }
}
