using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RecordReplay;

/// <summary>
/// The <c>--record</c> and <c>--replay</c> transfer hook (ADR-1135). An HTTP transfer whose request has an exchange in
/// the replay directory is answered from it without contacting the server (section 5); one that has none fails with
/// exit 7 unless a record directory is given too, when it goes out. A transfer that goes out with a record directory
/// is written on as received and, when a response arrived, recorded (section 4). A transfer that is not HTTP runs
/// as it would without the hook.
/// </summary>
/// <param name="replay">Where exchanges are answered from, or <see langword="null" /> without <c>--replay</c>.</param>
/// <param name="record">Where exchanges are recorded to, or <see langword="null" /> without <c>--record</c>.</param>
internal sealed class RecordReplayTransferHook(ExchangeDirectory? replay, ExchangeDirectory? record) : ITransferHook
{
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        var method = ExchangeKey.MethodOf(context);
        var url = context.Url.OriginalString;
        var key = ExchangeKey.For(method, url, context.PostData is { } data ? data.Span : default);
        if (replay is not null)
        {
            if (await replay.ReadAsync(key, context.CancellationToken) is { } exchange)
            {
                return await ReplayAsync(exchange, context);
            }

            if (record is null)
            {
                return TransferResult.Failure(CurlExitCode.CouldntConnect, $"No recorded exchange for {method} {url} in {replay.Path}");
            }
        }

        return await RecordAsync(record!, new RecordedExchange(method, url, 0, [], []), key, context, next);
    }

    private static async Task<TransferResult> ReplayAsync(RecordedExchange exchange, ITransferContext context)
    {
        await WriteIfGivenAsync(context.HeaderOutput, exchange.Head, context.CancellationToken);
        await WriteIfGivenAsync(context.DumpHeaderOutput, exchange.Head, context.CancellationToken);
        await context.Output.WriteAsync(exchange.Body, context.CancellationToken);
        var headers = RecordedHead.FieldsOfLastResponse(exchange.Head);
        return TransferResult.Success(exchange.Body.Length) with
        {
            Report = new TransferReport
            {
                ResponseCode = exchange.StatusCode,
                ResponseHeaders = headers,
                Method = exchange.Method,
                EffectiveUrl = exchange.Url,
                ContentType = headers.FirstOrDefault(field => field.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value,
            },
        };
    }

    private static async Task<TransferResult> RecordAsync(
        ExchangeDirectory directory,
        RecordedExchange request,
        string key,
        ITransferContext context,
        Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        using MemoryStream body = new();
        using MemoryStream head = new();
        using MemoryStream dumpedHead = new();
        var result = await next(context.WithOutputs(body, head, context.DumpHeaderOutput is null ? null : dumpedHead));
        await WriteIfGivenAsync(context.HeaderOutput, head.ToArray(), context.CancellationToken);
        await WriteIfGivenAsync(context.DumpHeaderOutput, dumpedHead.ToArray(), context.CancellationToken);
        await context.Output.WriteAsync(body.ToArray(), context.CancellationToken);
        if (result is { IsSuccess: true, Report: { } report })
        {
            await TryRecordAsync(directory, key, request with { StatusCode = report.ResponseCode, Head = head.ToArray(), Body = body.ToArray() }, context.CancellationToken);
        }

        return result;
    }

    private static async Task TryRecordAsync(ExchangeDirectory directory, string key, RecordedExchange exchange, CancellationToken cancellationToken)
    {
        try
        {
            await directory.WriteAsync(key, exchange, cancellationToken);
        }
        catch (IOException)
        {
            // A record directory that cannot be written leaves the transfer as it was.
        }
    }

    private static async Task WriteIfGivenAsync(Stream? output, byte[] bytes, CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await output.WriteAsync(bytes, cancellationToken);
        }
    }

    /// <summary>Reads header fields from a recorded head.</summary>
    private static class RecordedHead
    {
        /// <summary>The fields of the last response in <paramref name="head" />, after its last status line.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> FieldsOfLastResponse(byte[] head)
        {
            var lines = Encoding.Latin1.GetString(head).Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            var start = lines.FindLastIndex(line => line.StartsWith("HTTP/", StringComparison.Ordinal)) + 1;
            return [.. lines.Skip(start)
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2)
                .Select(parts => new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()))];
        }
    }
}
