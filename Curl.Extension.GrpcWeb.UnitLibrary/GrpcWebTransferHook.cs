using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.GrpcWeb;

/// <summary>
/// What <c>--grpc-web</c> does to an HTTP transfer (ADR-1132): sends the <c>-d</c> data, or an empty message when
/// there is none, as one framed <c>application/grpc-web+proto</c> message (PROTOCOL-WEB.md, "Content-Type" and
/// "Message framing") over whichever HTTP version curl's options pick ("HTTP wire protocols"), and writes the
/// messages of a 200 gRPC-Web reply without their frames before checking its <c>grpc-status</c>
/// (<see cref="GrpcWebStatus" />). Any other reply is written as curl received it. A transfer that is not HTTP runs
/// unchanged; a <c>-F</c> body fails it with exit 2 before anything is sent.
/// </summary>
internal sealed class GrpcWebTransferHook : ITransferHook
{
    /// <summary>The media type of the request body.</summary>
    internal const string MediaType = "application/grpc-web+proto";

    private const string ReplyMediaType = "application/grpc-web";

    private const string TextReplyMediaType = "application/grpc-web-text";

    private const int Ok = 200;

    private static readonly KeyValuePair<string, string>[] RequestFields =
    [
        new("Accept", MediaType),
        new("X-Grpc-Web", "1"),
    ];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is not { } http)
        {
            return await next(context).ConfigureAwait(false);
        }

        if (http.Body is StreamBody)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, "--grpc-web: the request message comes from -d or --data-binary, not -F");
        }

        ReadOnlyMemory<byte> message = http.Body is BytesBody given ? given.Content : ReadOnlyMemory<byte>.Empty;
        HttpRequestOptions request = http with
        {
            Body = new BytesBody(GrpcWebFraming.Frame(message.Span), MediaType),
            Headers = [.. http.Headers, .. RequestFields.Where(field => !GivesField(http.Headers, field.Key)).Select(field => field.Key + ": " + field.Value)],
        };
        using MemoryStream reply = new();
        TransferResult result = await next(new GrpcWebRequestContext(context, reply, request)).ConfigureAwait(false);
        return await AnswerAsync(context, reply.GetBuffer().AsMemory(0, (int)reply.Length), result).ConfigureAwait(false);
    }

    private static async ValueTask<TransferResult> AnswerAsync(ITransferContext context, ReadOnlyMemory<byte> body, TransferResult result)
    {
        if (!IsGrpcWebReply(result))
        {
            await context.Output.WriteAsync(body, context.CancellationToken).ConfigureAwait(false);
            return result;
        }

        GrpcWebReply reply;
        try
        {
            reply = GrpcWebFraming.Parse(body);
        }
        catch (FormatException malformed)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, malformed.Message, result.BytesTransferred) with { Report = result.Report };
        }
        catch (NotSupportedException compressed)
        {
            return TransferResult.Failure(CurlExitCode.BadContentEncoding, compressed.Message, result.BytesTransferred) with { Report = result.Report };
        }

        foreach (ReadOnlyMemory<byte> message in reply.Messages)
        {
            await context.Output.WriteAsync(message, context.CancellationToken).ConfigureAwait(false);
        }

        return GrpcWebStatus.Check(reply.Trailers ?? result.Report!.ResponseHeaders, result);
    }

    private static bool IsGrpcWebReply(TransferResult result) =>
        result is { IsSuccess: true, Report.ResponseCode: Ok } && IsBinaryGrpcWebType(result.Report.ContentType);

    private static bool IsBinaryGrpcWebType(string? type) =>
        type is not null
        && type.StartsWith(ReplyMediaType, StringComparison.OrdinalIgnoreCase)
        && !type.StartsWith(TextReplyMediaType, StringComparison.OrdinalIgnoreCase);

    private static bool GivesField(IEnumerable<string> headers, string name) =>
        headers.Any(header => header.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase));
}
