using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib556.c</c>: a verbose <c>CURLOPT_CONNECT_ONLY</c> perform, then <c>GET /&lt;testnum&gt; HTTP/1.1</c> with
/// <c>Host: ninja</c> sent with <c>curl_easy_send</c> and the reply read with <c>curl_easy_recv</c> to stdout
/// until the peer closes; case 696 does it twice on the same handle. A failed send or receive answers
/// <see cref="LibTestErrorCodes.Failure"/>.
/// </summary>
public sealed class Lib556 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib556";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
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
            bool connected = await steps.SetOptionsAndPerformAsync(
                easy,
                [(CurlOption.Url, context.Url), (CurlOption.ConnectOnly, 1L), (CurlOption.Verbose, 1L)],
                cancellationToken).ConfigureAwait(false);
            for (int transfer = 1; connected; transfer++)
            {
                steps.Result = await ExchangeAsync(easy, context.TestNumber, context.StandardOutput, cancellationToken).ConfigureAwait(false);
                connected = steps.Result == CurlCode.Ok && context.TestNumber == 696 && transfer == 1
                    && await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The raw exchange: sends the request a part at a time and reads like crazy, each received byte to
    /// <paramref name="output"/>, until a receive answers <see cref="CurlCode.Ok"/> with nothing (the peer closed)
    /// or a call fails.
    /// </summary>
    /// <param name="easy">The connected handle.</param>
    /// <param name="testNumber">The case's number, the path requested.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns><see cref="CurlCode.Ok"/>, or <see cref="LibTestErrorCodes.Failure"/> when a call failed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<CurlCode> ExchangeAsync(CurlEasy easy, int testNumber, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(output);
        ReadOnlyMemory<byte> request = Encoding.ASCII.GetBytes($"GET /{testNumber} HTTP/1.1\r\nHost: ninja\r\n\r\n");
        byte[] buffer = new byte[1024];
        CurlConnectOnlyResult read;
        do
        {
            ReadOnlyMemory<byte>? unsent = await SendSomeAsync(easy, request, cancellationToken).ConfigureAwait(false);
            if (unsent is null)
            {
                return LibTestErrorCodes.Failure;
            }

            request = unsent.Value;
            read = await ReceiveSomeAsync(easy, buffer, output, cancellationToken).ConfigureAwait(false);
        }
        while ((read.Result == CurlCode.Ok && read.ByteCount > 0) || read.Result == CurlCode.Again);

        return read.Result == CurlCode.Ok ? CurlCode.Ok : LibTestErrorCodes.Failure;
    }

    /// <summary>One <c>curl_easy_send</c> of what is left of the request, when anything is.</summary>
    /// <param name="easy">The connected handle.</param>
    /// <param name="request">The bytes not yet sent.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>The bytes still unsent, or <see langword="null"/> when the send failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public static async ValueTask<ReadOnlyMemory<byte>?> SendSomeAsync(CurlEasy easy, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(easy);
        if (request.IsEmpty)
        {
            return request;
        }

        var sent = await easy.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return sent.Result switch
        {
            CurlCode.Ok => request[sent.ByteCount..],
            CurlCode.Again => request,
            _ => (ReadOnlyMemory<byte>?)null,
        };
    }

    /// <summary>One <c>curl_easy_recv</c>, its bytes written to <paramref name="output"/>; on <see cref="CurlCode.Again"/> it yields first.</summary>
    /// <param name="easy">The connected handle.</param>
    /// <param name="buffer">Where the bytes are received.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="cancellationToken">Stops the receive.</param>
    /// <returns>The receive's code and the bytes written, 0 unless the code is <see cref="CurlCode.Ok"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<CurlConnectOnlyResult> ReceiveSomeAsync(CurlEasy easy, byte[] buffer, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(output);
        var read = await easy.RecvAsync(buffer, cancellationToken).ConfigureAwait(false);
        int received = read.Result == CurlCode.Ok ? read.ByteCount : 0;
        await output.WriteAsync(buffer.AsMemory(0, received), cancellationToken).ConfigureAwait(false);
        if (read.Result == CurlCode.Again)
        {
            await Task.Yield();
        }

        return new CurlConnectOnlyResult(read.Result, received);
    }
}
