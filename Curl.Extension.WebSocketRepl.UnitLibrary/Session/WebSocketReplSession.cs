using System.Buffers;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl.Session;

/// <summary>
/// An interactive WebSocket session over a connection whose opening handshake has succeeded: each typed line
/// is sent as a masked frame (<see cref="WebSocketReplLine" />), each received message is shown, each ping is
/// answered with a pong (RFC 6455 sections 5.5.2 and 5.5.3), and the closing handshake ends it (sections 5.5.1
/// and 7).
/// </summary>
/// <remarks>
/// Lines are read and frames received at the same time. The end of input sends a close with code 1000, as
/// <c>/close</c> does, and the session then reads on until the server answers it or the connection ends. A
/// close from the server is answered with its own status code and ends the session at once.
/// </remarks>
/// <param name="connection">The connection, its opening handshake done.</param>
/// <param name="randomSource">Supplies each sent frame's mask.</param>
public sealed class WebSocketReplSession(IConnection connection, IWebSocketRandomSource randomSource) : IDisposable
{
    private static readonly byte[] ProtocolError = [0x03, 0xEA];

    private readonly SemaphoreSlim sending = new(1, 1);

    private readonly WsFrameMetadataDecoder decoder = new();

    private readonly ArrayBufferWriter<byte> closePayload = new();

    private bool isCloseSent;

    /// <summary>Runs the session until it closes or the connection ends.</summary>
    /// <param name="input">The typed lines, standard input in the console.</param>
    /// <param name="output">Where received messages are shown, the transfer's output in the console.</param>
    /// <param name="alreadyReceived">Frames the server sent with its handshake response (<c>AlreadyReceived</c>).</param>
    /// <param name="cancellationToken">Cancels the session.</param>
    /// <returns>Why the session ended.</returns>
    public async Task<WebSocketReplOutcome> RunAsync(
        TextReader input, Stream output, ReadOnlyMemory<byte> alreadyReceived, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        using CancellationTokenSource inputStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task sendingLines = SendLinesAsync(input, inputStop.Token);
        Task<WebSocketReplOutcome> receiving = ReceiveAsync(new WebSocketReplOutput(output), alreadyReceived, cancellationToken);

        // Waits for receiving to end however it ends, then stops the lines before passing on its outcome or fault.
        await Task.WhenAny(receiving).ConfigureAwait(false);
        await inputStop.CancelAsync().ConfigureAwait(false);
        await AwaitStoppedAsync(sendingLines).ConfigureAwait(false);
        return await receiving.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => sending.Dispose();

    private static async Task AwaitStoppedAsync(Task sendingLines)
    {
        try
        {
            await sendingLines.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The session ended while a line was awaited.
        }
    }

    private async Task SendLinesAsync(TextReader input, CancellationToken cancellationToken)
    {
        while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            (WsOpcode opcode, byte[] payload) = WebSocketReplLine.Read(line);
            await SendAsync(opcode, payload, cancellationToken).ConfigureAwait(false);
            if (opcode == WsOpcode.Close)
            {
                return;
            }
        }

        await SendAsync(WsOpcode.Close, WebSocketReplLine.NormalClosure, cancellationToken).ConfigureAwait(false);
    }

    // Sends one frame, unless it is a second close: after its close an endpoint sends no other (section 5.5.1).
    private async ValueTask<bool> SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await sending.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (opcode == WsOpcode.Close && isCloseSent)
            {
                return false;
            }

            isCloseSent |= opcode == WsOpcode.Close;
            byte[] frame = WsFrameEncoder.Encode(opcode, payload.Span, randomSource);
            await connection.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            sending.Release();
        }
    }

    private async Task<WebSocketReplOutcome> ReceiveAsync(WebSocketReplOutput output, ReadOnlyMemory<byte> alreadyReceived, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16384];
        ReadOnlyMemory<byte> received = alreadyReceived;
        while (true)
        {
            WebSocketReplOutcome? outcome = await HandleAsync(decoder.Decode(received.Span), output, cancellationToken).ConfigureAwait(false);
            if (outcome is not null)
            {
                return outcome;
            }

            int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return new WebSocketReplOutcome(WebSocketReplEnding.ConnectionEnded, null);
            }

            received = buffer.AsMemory(0, read);
        }
    }

    private async ValueTask<WebSocketReplOutcome?> HandleAsync(WsDecodedFrames decoded, WebSocketReplOutput output, CancellationToken cancellationToken)
    {
        foreach (WsFrameRun run in decoded.Runs)
        {
            if (await HandleRunIsCloseEndAsync(run, output, cancellationToken).ConfigureAwait(false))
            {
                return await AnswerCloseAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return await AnswerPingAndViolationAsync(decoded, cancellationToken).ConfigureAwait(false);
    }

    // Shows a message's run or keeps a close frame's; true once the close frame has ended.
    private async ValueTask<bool> HandleRunIsCloseEndAsync(WsFrameRun run, WebSocketReplOutput output, CancellationToken cancellationToken)
    {
        if (run.Opcode != WsOpcode.Close)
        {
            await output.ShowAsync(run, cancellationToken).ConfigureAwait(false);
            return false;
        }

        closePayload.Write(run.Payload.Span);
        return run.BytesLeft == 0;
    }

    private async ValueTask<WebSocketReplOutcome?> AnswerPingAndViolationAsync(WsDecodedFrames decoded, CancellationToken cancellationToken)
    {
        if (decoded.CompletedPing is { } ping)
        {
            await SendAsync(WsOpcode.Pong, ping, cancellationToken).ConfigureAwait(false);
        }

        return decoded.ViolationMessage is { } violation
            ? await FailAsync(violation, cancellationToken).ConfigureAwait(false)
            : null;
    }

    // Answers with the status code the server's close carried, or with none when it carried none (section 5.5.1).
    private async ValueTask<WebSocketReplOutcome> AnswerCloseAsync(CancellationToken cancellationToken)
    {
        byte[] statusCode = closePayload.WrittenCount >= 2 ? closePayload.WrittenSpan[..2].ToArray() : [];
        bool isAnswer = await SendAsync(WsOpcode.Close, statusCode, cancellationToken).ConfigureAwait(false);
        return new WebSocketReplOutcome(isAnswer ? WebSocketReplEnding.ClosedByServer : WebSocketReplEnding.ClosedByClient, null);
    }

    private async ValueTask<WebSocketReplOutcome> FailAsync(string violation, CancellationToken cancellationToken)
    {
        await SendAsync(WsOpcode.Close, ProtocolError, cancellationToken).ConfigureAwait(false);
        return new WebSocketReplOutcome(WebSocketReplEnding.ProtocolViolation, violation);
    }
}
