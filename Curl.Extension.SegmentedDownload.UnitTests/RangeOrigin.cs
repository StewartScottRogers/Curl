using System.Collections.Concurrent;
using System.Text;

namespace Curl.Extension.SegmentedDownload.UnitTests;

/// <summary>
/// Stands in for curl's handler talking to an origin that serves <paramref name="representation" /> and answers a
/// single byte range with 206 and <c>Content-Range</c> (RFC 9110 §14.2, §14.4, §15.3.7), an unconditional request
/// with 200, and a range whose <c>If-Range</c> does not match <see cref="ETag" /> with the whole 200 (§13.1.5).
/// </summary>
internal sealed class RangeOrigin(string representation)
{
    /// <summary>Gets every request it received, in the order they arrived.</summary>
    public ConcurrentQueue<ITransferContext> Requests { get; } = new();

    /// <summary>Gets or sets the strong entity tag it sends, or <see langword="null" /> for none.</summary>
    public string? ETag { get; set; } = "\"v1\"";

    /// <summary>Gets or sets whether it ignores <c>Range</c> and always answers 200.</summary>
    public bool IgnoresRange { get; set; }

    /// <summary>Gets or sets a rewrite of each response before it is sent, by the range it answers (null for 200).</summary>
    public Func<string?, Response, Response> Rewrite { get; set; } = (_, response) => response;

    public async ValueTask<TransferResult> Invoke(ITransferContext context)
    {
        Requests.Enqueue(context);
        var range = Field(context, "Range");
        var ifRange = Field(context, "If-Range");
        var response = range is null || IgnoresRange || (ifRange is not null && ifRange != ETag)
            ? Rewrite(null, Whole())
            : Rewrite(range, Part(range["bytes=".Length..]));
        var head = Encoding.Latin1.GetBytes($"HTTP/1.1 {response.Code} X\r\n" + string.Concat(response.Fields.Select(field => field.Key + ": " + field.Value + "\r\n")) + "\r\n");
        await context.HeaderOutput!.WriteAsync(head);
        if (context.DumpHeaderOutput is { } dump)
        {
            await dump.WriteAsync(head);
        }

        await context.Output.WriteAsync(Encoding.Latin1.GetBytes(response.Body));
        return response.Result ?? TransferResult.Success(response.Body.Length) with
        {
            Report = new TransferReport { ResponseCode = response.Code, ResponseHeaders = response.Fields },
        };
    }

    /// <summary>Gives the value of the request header field <paramref name="name" /> the context carries.</summary>
    public static string? Field(ITransferContext context, string name) =>
        context.Http!.Headers.Select(line => line.Split(": ", 2)).FirstOrDefault(parts => parts[0] == name)?[1];

    private Response Whole() => new(200, Validators(), representation);

    private Response Part(string spec)
    {
        var bounds = spec.Split('-');
        var first = int.Parse(bounds[0], System.Globalization.CultureInfo.InvariantCulture);
        var last = int.Parse(bounds[1], System.Globalization.CultureInfo.InvariantCulture);
        return new(206, [.. Validators(), new("Content-Range", $"bytes {first}-{last}/{representation.Length}")], representation[first..(last + 1)]);
    }

    private KeyValuePair<string, string>[] Validators() => ETag is null ? [] : [new("ETag", ETag)];

    /// <summary>One response the origin sends.</summary>
    internal sealed record Response(int Code, KeyValuePair<string, string>[] Fields, string Body, TransferResult? Result = null);
}
