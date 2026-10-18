using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2700.c</c> (cases 2700 to 2723): a verbose connect-only WebSocket transfer (<c>CURLOPT_CONNECT_ONLY</c> 2) of
/// the case's URL as <c>client/test2700</c>, traced by <c>libtest_debug_cb</c> without hex and with times, and with
/// <c>CURLWS_NOAUTOPONG</c> on unless the case is 2708. When it succeeds, frames are received until a CLOSE: each
/// frame's header (an empty receive) is written to stdout as its kind and its bytes left, then echoed with a
/// <c>CURLWS_OFFSET</c> send of that size; its payload is received in parts of at most 256 bytes, each written to stdout
/// and sent back; then a newline. A receive or send answering <c>CURLE_AGAIN</c> is retried; any other failure writes
/// its line to stderr and is the program's code.
/// </summary>
/// <param name="timeProvider">The time the trace lines carry.</param>
public sealed class Lib2700(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The size of the buffer each payload part is received into.</summary>
    public const int ChunkSize = 256;

    /// <summary>The case whose transfer keeps libcurl's automatic PONG answers.</summary>
    public const int AutoPongCase = 2708;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib2700";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">The time the trace lines carry.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        // curl_global_trace("ws") only picks which traces show; Libcurl's answers CURLE_OK.
        _ = CurlGlobal.Trace("ws");
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Useragent, "client/test2700");
            _ = easy.SetOption(CurlOption.Debugdata, trace);
            _ = easy.SetOption(CurlOption.Debugfunction, (DebugCallback)trace.Trace);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.ConnectOnly, 2L);
            if (context.TestNumber != AutoPongCase)
            {
                _ = easy.SetOption(CurlOption.WsOptions, (long)WebSocketOptionBits.NoAutoPong);
            }

            steps.Result = await new Lib2700Frames(context, easy).RunAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>descr_flags</c>: the kind a frame's flags name, such as <c>txt fin</c> or <c>close</c>.</summary>
    /// <param name="flags">The frame's flags.</param>
    /// <returns>The kind, or an empty string for flags naming none.</returns>
    public static string DescribeFlags(CurlWebSocketFlags flags)
    {
        string fragment = (flags & CurlWebSocketFlags.Continuation) != 0 ? " ---" : " fin";
        foreach (var (flag, kind, fragmented) in FrameKinds)
        {
            if ((flags & flag) != 0)
            {
                return fragmented ? kind + fragment : kind;
            }
        }

        return string.Empty;
    }

    /// <summary><c>descr_flags</c>'s checks in order: each flag, its kind, and whether the kind ends in its fragment.</summary>
    private static readonly (CurlWebSocketFlags Flag, string Kind, bool Fragmented)[] FrameKinds =
    [
        (CurlWebSocketFlags.Text, "txt", true),
        (CurlWebSocketFlags.Binary, "bin", true),
        (CurlWebSocketFlags.Ping, "ping", false),
        (CurlWebSocketFlags.Pong, "pong", false),
        (CurlWebSocketFlags.Close, "close", false),
    ];

    /// <summary>Writes a failed call's line to stderr, as <c>%s:%d curl_ws_...() failed with code %d (%s)</c>.</summary>
    /// <param name="error">The program's stderr.</param>
    /// <param name="function">The failing call, such as <c>curl_ws_recv</c>.</param>
    /// <param name="code">The call's code.</param>
    /// <param name="file">The calling file, filled in by the compiler.</param>
    /// <param name="line">The calling line, filled in by the compiler.</param>
    /// <returns><paramref name="code"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static CurlCode ReportFailure(TextWriter error, string function, CurlCode code, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(error);
        error.Write(string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(file)}:{line} {function}() failed with code {(int)code} ({CurlErrorText.Easy(code)})\n"));
        return code;
    }
}
