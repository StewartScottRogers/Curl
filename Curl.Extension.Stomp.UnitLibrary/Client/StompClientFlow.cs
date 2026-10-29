using Curl.Extension.Stomp.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Stomp.Client;

/// <summary>
/// The STOMP 1.2 exchange over any byte transport (ADR-1059): <c>CONNECT</c> and <c>CONNECTED</c>, then with a body
/// <c>SEND</c> and a <c>DISCONNECT</c> whose <c>RECEIPT</c> confirms the server processed it, otherwise
/// <c>SUBSCRIBE</c> and each <c>MESSAGE</c> body written to the output until the server closes the connection.
/// An <c>ERROR</c> frame, or any frame the exchange does not expect, ends the transfer with exit 8.
/// </summary>
internal sealed class StompClientFlow(IConnection transport, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
{
    /// <summary>The <c>id</c> of the one subscription the flow makes.</summary>
    internal const string SubscriptionIdentifier = "0";

    /// <summary>The <c>receipt</c> the flow asks for on <c>DISCONNECT</c>.</summary>
    internal const string DisconnectReceipt = "disconnect";

    private readonly StompFrameReader reader = new(transport, cancellationToken);

    /// <summary>The body bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>Runs the flow: sends <paramref name="body"/> to <paramref name="destination"/>, or subscribes to it when there is none.</summary>
    internal async ValueTask RunAsync(StompFrame connect, string destination, ReadOnlyMemory<byte>? body)
    {
        await SendAsync(connect).ConfigureAwait(false);
        StompFrame connected = await ReceiveAsync("CONNECTED").ConfigureAwait(false);
        if (connected.HeaderValue("version") != "1.2")
        {
            throw new StompClientException(CurlExitCode.WeirdServerReply, StompClientMessages.UnsupportedVersion(connected.HeaderValue("version")));
        }

        if (body is { } data)
        {
            await SendAsync(SendFrame(destination, data)).ConfigureAwait(false);
            await SendAsync(new StompFrame("DISCONNECT", [new("receipt", DisconnectReceipt)], [])).ConfigureAwait(false);
            await ReceiveReceiptAsync().ConfigureAwait(false);
            return;
        }

        await SendAsync(new StompFrame("SUBSCRIBE", [new("id", SubscriptionIdentifier), new("destination", destination), new("ack", "auto")], [])).ConfigureAwait(false);
        await WriteMessagesUntilClosedAsync().ConfigureAwait(false);
    }

    /// <summary>Gives the <c>SEND</c> frame for <paramref name="body"/>, with its <c>content-length</c> so the body may hold NUL.</summary>
    internal static StompFrame SendFrame(string destination, ReadOnlyMemory<byte> body) =>
        new("SEND", [new("destination", destination), new("content-length", body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))], body.ToArray());

    private async ValueTask SendAsync(StompFrame frame)
    {
        try
        {
            await transport.WriteAsync(StompFrameEncoding.Encode(frame), cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new StompClientException(CurlExitCode.SendError, StompClientMessages.SendFailed(frame.Command));
        }
    }

    private async ValueTask<StompFrame?> ReadFrameAsync()
    {
        try
        {
            return await reader.ReadAsync().ConfigureAwait(false);
        }
        catch (FormatException malformed)
        {
            throw new StompClientException(CurlExitCode.WeirdServerReply, malformed.Message);
        }
        catch (IOException)
        {
            throw new StompClientException(CurlExitCode.RecvError, StompClientMessages.Disconnected);
        }
    }

    private async ValueTask<StompFrame> ReceiveAsync(string expectedCommand)
    {
        StompFrame frame = await ReadFrameAsync().ConfigureAwait(false)
            ?? throw new StompClientException(CurlExitCode.RecvError, StompClientMessages.Disconnected);
        return frame.Command == expectedCommand ? frame : throw Unexpected(frame);
    }

    private async ValueTask ReceiveReceiptAsync()
    {
        StompFrame receipt = await ReceiveAsync("RECEIPT").ConfigureAwait(false);
        if (receipt.HeaderValue("receipt-id") != DisconnectReceipt)
        {
            throw Unexpected(receipt);
        }
    }

    private async ValueTask WriteMessagesUntilClosedAsync()
    {
        for (StompFrame? frame = await ReadFrameAsync().ConfigureAwait(false); frame is not null; frame = await ReadFrameAsync().ConfigureAwait(false))
        {
            if (frame.Command != "MESSAGE")
            {
                throw Unexpected(frame);
            }

            await WriteOutputAsync(frame.Body).ConfigureAwait(false);
        }
    }

    private static StompClientException Unexpected(StompFrame frame) =>
        new(CurlExitCode.WeirdServerReply, frame.Command == "ERROR" ? StompClientMessages.ServerError(frame.HeaderValue("message")) : StompClientMessages.UnexpectedFrame(frame.Command));

    private async ValueTask WriteOutputAsync(byte[] body)
    {
        try
        {
            await output.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new StompClientException(CurlExitCode.WriteError, StompClientMessages.OutputWriteFailed(body.Length));
        }

        BytesWritten += body.Length;
        progress.ReportDownloaded(BytesWritten, null);
    }
}
