using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2301.c</c>: a verbose WebSocket transfer of the case's URL as <c>webbie-sox/3</c> in <c>CURLWS_RAW_MODE</c>, whose
/// write callback writes each chunk's size and bytes in hex to stderr and, for a chunk starting with a PING frame
/// (<c>0x89</c>), sends back a bare PONG with <c>curl_ws_send</c>, failing the transfer when that send fails. The transfer's
/// code is written as <c>curl_easy_perform() returned N</c> and is the program's. A library that gives no handle skips the
/// transfer and the program succeeds.
/// </summary>
public sealed class Lib2301 : ILibTest
{
    private static readonly byte[] Pong = [0x8a, 0x00];

    /// <inheritdoc/>
    public string ProgramName => "lib2301";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Useragent, "webbie-sox/3");
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.WsOptions, (long)WebSocketOptionBits.RawMode);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data => Write(context.StandardError, easy, data)));
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"curl_easy_perform() returned {(int)steps.Result}\n"));
        }
    }

    /// <summary><c>t2301_write_cb</c>: writes the chunk in hex and answers a PING with a PONG.</summary>
    /// <param name="error">The program's stderr.</param>
    /// <param name="easy">The transfer's handle, which sends the PONG.</param>
    /// <param name="data">The chunk.</param>
    /// <returns>The chunk's size, or 0 when the PONG could not be sent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static long Write(TextWriter error, CurlEasy? easy, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(error);
        var line = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"Called CURLOPT_WRITEFUNCTION with {data.Length} bytes: "));
        foreach (byte octet in data)
        {
            _ = line.Append(octet.ToString("x2", CultureInfo.InvariantCulture)).Append(' ');
        }

        error.Write(line.Append('\n').ToString());
        long written = data.Length;
        if (data.Length > 0 && data[0] == 0x89)
        {
            error.Write("send back a simple PONG\n");
            written = easy.WebSocketSend(Pong, out _, 0, CurlWebSocketFlags.None) == CurlCode.Ok ? written : 0;
        }

        if (written != data.Length)
        {
            error.Write("returns error from callback\n");
        }

        return written;
    }
}
