using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2304.c</c>: a verbose connect-only WebSocket transfer (<c>CURLOPT_CONNECT_ONLY</c> 2) of the case's URL as
/// <c>websocket/2304</c>, its code written as <c>curl_easy_perform() returned N</c> and the program's. When it succeeds,
/// up to eleven rounds of: receive any frame, send a PING of <see cref="Payload"/>, receive its PONG and wait two seconds;
/// then a CLOSE. A failing PING send ends the rounds; a missing or wrong PONG writes <c>Connection closed</c> and ends them
/// without the CLOSE. The rounds' sends and receives are <c>first.c</c>'s <c>ws_send_ping</c>, <c>ws_recv_pong</c> and
/// <c>ws_close</c>, writing their lines to stderr. A library that gives no handle skips the transfer and the program
/// succeeds.
/// </summary>
/// <param name="timeProvider">The time the two-second waits are measured on.</param>
public sealed class Lib2304(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The PING's payload, which the PONG must echo.</summary>
    public const string Payload = "foobar";

    /// <summary>The rounds the program makes at most, upstream's <c>while(i++ &lt; 10)</c>.</summary>
    public const int Rounds = 11;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib2304";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, for the waits.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
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
            _ = easy.SetOption(CurlOption.Useragent, "websocket/2304");
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.ConnectOnly, 2L);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"curl_easy_perform() returned {(int)steps.Result}\n"));
            if (steps.Result == CurlCode.Ok)
            {
                await WebSocketRoundsAsync(context, easy, clock, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask WebSocketRoundsAsync(LibTestContext context, CurlEasy easy, LibTestClock clock, CancellationToken cancellationToken)
    {
        TextWriter error = context.StandardError;
        error.Write("ws: websocket() starts\n");
        for (int round = 0; round < Rounds; round++)
        {
            await ReceiveAnyAsync(error, easy, cancellationToken).ConfigureAwait(false);
            error.Write("Send ping\n");
            if (await SendAsync(error, easy, Encoding.ASCII.GetBytes(Payload), CurlWebSocketFlags.Ping, cancellationToken).ConfigureAwait(false) != CurlCode.Ok)
            {
                return;
            }

            error.Write("Receive pong\n");
            if (!await ReceivePongAsync(error, easy, cancellationToken).ConfigureAwait(false))
            {
                using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                output.Write("Connection closed\n");
                return;
            }

            await clock.WaitAsync(2000, cancellationToken).ConfigureAwait(false);
        }

        _ = await SendAsync(error, easy, ReadOnlyMemory<byte>.Empty, CurlWebSocketFlags.Close, cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>recv_any</c>: receives one frame and, when it came, writes its size and flags.</summary>
    private static async ValueTask ReceiveAnyAsync(TextWriter error, CurlEasy easy, CancellationToken cancellationToken)
    {
        CurlWebSocketReceiveResult received = await easy.WebSocketRecvAsync(new byte[256], cancellationToken).ConfigureAwait(false);
        if (received.Result == CurlCode.Ok)
        {
            error.Write(string.Create(CultureInfo.InvariantCulture, $"recv_any: got {received.BytesReceived} bytes rflags {(int)received.Frame.Flags:x}\n"));
        }
    }

    /// <summary><c>ws_send_ping</c> and <c>ws_close</c>: sends one frame and writes the send's code and size.</summary>
    private static async ValueTask<CurlCode> SendAsync(TextWriter error, CurlEasy easy, ReadOnlyMemory<byte> payload, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        CurlWebSocketSendResult sent = await easy.WebSocketSendAsync(payload, 0, flags, cancellationToken).ConfigureAwait(false);
        error.Write(string.Create(CultureInfo.InvariantCulture, $"ws: curl_ws_send returned {(int)sent.Result}, sent {sent.BytesSent}\n"));
        return sent.Result;
    }

    /// <summary><c>ws_recv_pong</c>: whether the next frame is a PONG echoing <see cref="Payload"/>.</summary>
    private static async ValueTask<bool> ReceivePongAsync(TextWriter error, CurlEasy easy, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[256];
        CurlWebSocketReceiveResult received = await easy.WebSocketRecvAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (received.Result != CurlCode.Ok)
        {
            error.Write(string.Create(CultureInfo.InvariantCulture, $"ws: curl_ws_recv returned {(int)received.Result}, received {received.BytesReceived}\n"));
            return false;
        }

        if (!received.Frame.Flags.HasFlag(CurlWebSocketFlags.Pong))
        {
            error.Write(string.Create(CultureInfo.InvariantCulture, $"recv_pong: wrong frame, got {received.BytesReceived} bytes rflags {(int)received.Frame.Flags:x}\n"));
            return false;
        }

        error.Write("ws: got PONG back\n");
        bool same = Encoding.ASCII.GetString(buffer, 0, received.BytesReceived) == Payload;
        error.Write(same ? "ws: got the same payload back\n" : "ws: did NOT get the same payload back\n");
        return same;
    }
}
