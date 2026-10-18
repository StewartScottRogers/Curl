using System.Globalization;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2302.c</c>: a verbose WebSocket transfer of the case's URL as <c>webbie-sox/3</c>, whose write callback gathers
/// each chunk with the frame <c>curl_ws_meta</c> gives into <see cref="Lib2302Frames"/>, writing
/// <c>returns error from callback</c> when it could not hold a chunk (the callback still takes it). The transfer's code is
/// written as <c>curl_easy_perform() returned N</c> and is the program's; what is still held is flushed after the handle's
/// cleanup. A library that gives no handle skips the transfer and the program succeeds.
/// </summary>
public sealed class Lib2302 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2302";

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

        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        var frames = new Lib2302Frames(output, context.StandardError);
        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Useragent, "webbie-sox/3");
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data => Write(context.StandardError, frames, easy, data)));
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"curl_easy_perform() returned {(int)steps.Result}\n"));
        }

        frames.Flush();
    }

    /// <summary><c>t2302_write_cb</c>: holds the chunk with the transfer's current frame.</summary>
    /// <param name="error">The program's stderr.</param>
    /// <param name="frames">What the chunks are gathered into.</param>
    /// <param name="easy">The transfer's handle, asked for its frame.</param>
    /// <param name="data">The chunk.</param>
    /// <returns>The chunk's size, always.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> or <paramref name="frames"/> is <see langword="null"/>.</exception>
    public static long Write(TextWriter error, Lib2302Frames frames, CurlEasy? easy, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Add(data, easy.WebSocketMeta()) != data.Length)
        {
            error.Write("returns error from callback\n");
        }

        return data.Length;
    }
}
