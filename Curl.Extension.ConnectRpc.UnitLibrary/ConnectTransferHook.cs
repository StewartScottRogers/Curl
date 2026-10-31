using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ConnectRpc;

/// <summary>
/// What <c>--connect-rpc</c> does to an HTTP transfer (ADR-1139). A unary call POSTs the <c>-d</c> message as
/// <c>application/json</c> and writes the response unchanged ("Unary-Request", "Unary-Response"); a streaming call
/// POSTs the <c>-d</c> messages in envelopes as <c>application/connect+json</c> and writes each reply message on its
/// own line, failing with exit 22 when the end-of-stream message carries an error ("Streaming-Request",
/// "Streaming-Response"). Both send <c>Connect-Protocol-Version: 1</c>, and <c>Connect-Timeout-Ms</c> when
/// <c>--max-time</c> is given. A transfer that is not HTTP runs unchanged.
/// </summary>
/// <param name="value">The value given to <c>--connect-rpc</c>: <c>unary</c> or <c>stream</c>.</param>
internal sealed class ConnectTransferHook(string value) : ITransferHook
{
    /// <summary>The media type of a unary call's request ("Unary-Content-Type" with the JSON codec).</summary>
    internal const string UnaryMediaType = "application/json";

    /// <summary>The media type of a streaming call's request ("Streaming-Content-Type" with the JSON codec).</summary>
    internal const string StreamingMediaType = "application/connect+json";

    /// <summary>The largest <c>Connect-Timeout-Ms</c>, ten digits ("Timeout").</summary>
    private const long MaxTimeoutMilliseconds = 9_999_999_999;

    /// <summary>The message sent when <c>-d</c> gives none: the empty JSON object.</summary>
    private static readonly ReadOnlyMemory<byte> EmptyMessage = "{}"u8.ToArray();

    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        context.Http is { } http ? CallAsync(context, http, next) : next(context);

    /// <summary>Sends the call over an HTTP transfer, or fails with exit 2 when the call type or the message cannot be sent.</summary>
    private ValueTask<TransferResult> CallAsync(ITransferContext context, HttpRequestOptions http, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (StreamsFor(value) is not { } streams)
        {
            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, $"--connect-rpc: '{value}' is not a call type, give unary or stream"));
        }

        if (MessageOf(http.Body) is not { } message)
        {
            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, "--connect-rpc: give the request message with -d or --data-binary"));
        }

        return streams
            ? StreamAsync(context, http, message, next)
            : next(new ConnectRequestContext(context, context.Output, RequestOf(context, http, UnaryMediaType, message)));
    }

    /// <summary>Gives whether <paramref name="callType" /> names a streaming call, or <see langword="null" /> when it names neither type.</summary>
    private static bool? StreamsFor(string callType) => callType switch
    {
        "unary" => false,
        "stream" => true,
        _ => null,
    };

    /// <summary>Gives the request message <c>-d</c> gave, the empty object when none, or <see langword="null" /> for a body read from a stream.</summary>
    private static ReadOnlyMemory<byte>? MessageOf(HttpRequestBody? body) => body switch
    {
        null => EmptyMessage,
        BytesBody bytes => bytes.Content,
        _ => (ReadOnlyMemory<byte>?)null,
    };

    /// <summary>Sends a streaming call and writes its reply messages, one per line.</summary>
    private static async ValueTask<TransferResult> StreamAsync(
        ITransferContext context,
        HttpRequestOptions http,
        ReadOnlyMemory<byte> body,
        Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        IReadOnlyList<ReadOnlyMemory<byte>> messages;
        try
        {
            messages = ConnectRequestMessages.Split(body);
        }
        catch (FormatException invalid)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, invalid.Message);
        }

        using MemoryStream received = new();
        HttpRequestOptions request = RequestOf(context, http, StreamingMediaType, ConnectEnvelope.Encode(messages));
        TransferResult result = await next(new ConnectRequestContext(context, received, request)).ConfigureAwait(false);
        return result.IsSuccess ? await WriteReplyAsync(context, received.GetBuffer().AsMemory(0, (int)received.Length), result).ConfigureAwait(false) : result;
    }

    /// <summary>Writes the messages of a streaming response, failing when it carries an error or is not a Connect stream.</summary>
    private static async ValueTask<TransferResult> WriteReplyAsync(ITransferContext context, ReadOnlyMemory<byte> framed, TransferResult result)
    {
        ConnectStreamReply reply;
        try
        {
            reply = ConnectStreamReply.Read(framed.Span);
        }
        catch (FormatException invalid)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, invalid.Message, result.BytesTransferred);
        }

        await context.Output.WriteAsync(reply.Messages, context.CancellationToken).ConfigureAwait(false);
        return reply.Error is { } error ? TransferResult.Failure(CurlExitCode.HttpReturnedError, error, result.BytesTransferred) : result;
    }

    /// <summary>Gives the transfer's HTTP options with the Connect headers before its own and <paramref name="body" /> as the request body.</summary>
    private static HttpRequestOptions RequestOf(ITransferContext context, HttpRequestOptions http, string mediaType, ReadOnlyMemory<byte> body) => http with
    {
        Headers = ["Connect-Protocol-Version: 1", .. TimeoutHeaders(context.MaxTime), .. http.Headers],
        Body = new BytesBody(body, mediaType),
    };

    /// <summary>Gives the <c>Connect-Timeout-Ms</c> header for <paramref name="maxTime" />, rounded up to whole milliseconds, or none.</summary>
    private static IEnumerable<string> TimeoutHeaders(TimeSpan? maxTime) =>
        maxTime is { } limit ? [$"Connect-Timeout-Ms: {Math.Min((long)Math.Ceiling(limit.TotalMilliseconds), MaxTimeoutMilliseconds)}"] : [];
}
