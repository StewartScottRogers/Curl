using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Libcurl.Easy;
using Libcurl.WebSocket;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2700.c</c>'s transfer and echo loop over one handle: <c>curl_easy_perform</c>, then <c>recv_frame</c> until a
/// CLOSE frame, with its <c>recv_header</c>, <c>send_header</c>, <c>recv_chunk</c> and <c>send_chunk</c>.
/// </summary>
/// <param name="context">The program's context, whose stdout and stderr the loop writes.</param>
/// <param name="easy">The connect-only handle, its options set.</param>
public sealed class Lib2700Frames(LibTestContext context, CurlEasy easy)
{
    private readonly LibTestContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly CurlEasy easy = easy ?? throw new ArgumentNullException(nameof(easy));

    /// <summary>Performs the transfer and echoes frames until a CLOSE.</summary>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The program's code: the first failure, or <see cref="CurlCode.Ok"/>.</returns>
    public async ValueTask<CurlCode> RunAsync(CancellationToken cancellationToken)
    {
        CurlCode result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (result != CurlCode.Ok)
        {
            return Lib2700.ReportFailure(context.StandardError, "curl_easy_perform", result);
        }

        bool stop = false;
        while (!stop && result == CurlCode.Ok)
        {
            (result, stop) = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary><c>recv_frame</c>: one frame's header and payload, echoed; whether it was a CLOSE.</summary>
    private async ValueTask<(CurlCode Result, bool Stop)> ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        var (result, frame) = await ReceiveHeaderAsync(cancellationToken).ConfigureAwait(false);
        long bytesLeft = frame.BytesLeft;
        while (result == CurlCode.Ok && bytesLeft > 0)
        {
            (result, bytesLeft) = await ReceiveChunkAsync(frame.Flags, bytesLeft, cancellationToken).ConfigureAwait(false);
        }

        if (result != CurlCode.Ok)
        {
            return (result, false);
        }

        Write("\n");
        return (result, (frame.Flags & CurlWebSocketFlags.Close) != 0);
    }

    /// <summary><c>recv_header</c>: an empty receive giving the frame's flags and size, written and echoed.</summary>
    private async ValueTask<(CurlCode Result, CurlWebSocketFrame Frame)> ReceiveHeaderAsync(CancellationToken cancellationToken)
    {
        CurlWebSocketReceiveResult received = await ReceiveAsync(Memory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        if (received.Result != CurlCode.Ok)
        {
            return (Lib2700.ReportFailure(context.StandardError, "curl_ws_recv", received.Result), default);
        }

        CurlWebSocketFrame frame = received.Frame;
        Write(string.Create(CultureInfo.InvariantCulture, $"{Lib2700.DescribeFlags(frame.Flags)} [{frame.BytesLeft}]{(frame.BytesLeft > 0 ? " " : string.Empty)}"));
        CurlCode sent = await SendAsync(ReadOnlyMemory<byte>.Empty, frame.BytesLeft, frame.Flags | CurlWebSocketFlags.Offset, cancellationToken).ConfigureAwait(false);
        return (sent, frame);
    }

    /// <summary><c>recv_chunk</c>: up to <see cref="Lib2700.ChunkSize"/> payload bytes, written and sent back.</summary>
    private async ValueTask<(CurlCode Result, long BytesLeft)> ReceiveChunkAsync(CurlWebSocketFlags flags, long bytesLeft, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[Lib2700.ChunkSize];
        CurlWebSocketReceiveResult received = await ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (received.Result != CurlCode.Ok)
        {
            return (Lib2700.ReportFailure(context.StandardError, "curl_ws_recv", received.Result), bytesLeft);
        }

        int count = received.BytesReceived;
        context.StandardOutput.Write(buffer, 0, count);
        int sentOffset = 0;
        CurlCode result = CurlCode.Ok;
        while (result == CurlCode.Ok && sentOffset < count)
        {
            CurlWebSocketSendResult sent = await SendPartAsync(buffer.AsMemory(sentOffset, count - sentOffset), flags, cancellationToken).ConfigureAwait(false);
            result = sent.Result;
            sentOffset += sent.BytesSent;
        }

        return (result, bytesLeft - count);
    }

    /// <summary><c>send_header</c>: a send retried while it answers <c>CURLE_AGAIN</c>, its failure reported.</summary>
    private async ValueTask<CurlCode> SendAsync(ReadOnlyMemory<byte> payload, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        CurlWebSocketSendResult sent;
        do
        {
            sent = await easy.WebSocketSendAsync(payload, fragmentSize, flags, cancellationToken).ConfigureAwait(false);
        }
        while (sent.Result == CurlCode.Again);

        return sent.Result == CurlCode.Ok ? CurlCode.Ok : Lib2700.ReportFailure(context.StandardError, "curl_ws_send", sent.Result);
    }

    /// <summary><c>send_chunk</c>: a payload part sent, retried while it answers <c>CURLE_AGAIN</c>, its failure reported.</summary>
    private async ValueTask<CurlWebSocketSendResult> SendPartAsync(ReadOnlyMemory<byte> part, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        CurlWebSocketSendResult sent;
        do
        {
            sent = await easy.WebSocketSendAsync(part, 0, flags, cancellationToken).ConfigureAwait(false);
        }
        while (sent.Result == CurlCode.Again);

        return sent.Result == CurlCode.Ok
            ? sent
            : new CurlWebSocketSendResult(Lib2700.ReportFailure(context.StandardError, "curl_ws_send", sent.Result), 0);
    }

    /// <summary>A receive retried while it answers <c>CURLE_AGAIN</c>, as upstream's <c>goto retry</c>.</summary>
    private async ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        CurlWebSocketReceiveResult received;
        do
        {
            received = await easy.WebSocketRecvAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        while (received.Result == CurlCode.Again);

        return received;
    }

    private void Write(string text) => context.StandardOutput.Write(Encoding.ASCII.GetBytes(text));
}
