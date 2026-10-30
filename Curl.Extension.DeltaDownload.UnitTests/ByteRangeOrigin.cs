using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

/// <summary>
/// Stands in for curl's handler talking to an origin that serves <paramref name="representation" />: a single byte range
/// gets 206 with <c>Content-Range</c> (RFC 9110 §14.2, §14.4), a request without one gets 200 and the whole of it.
/// </summary>
internal sealed class ByteRangeOrigin(byte[] representation)
{
    /// <summary>Gets the <c>Range</c> of every request it received, null for none, in order.</summary>
    public List<string?> Ranges { get; } = [];

    /// <summary>Gets or sets a rewrite of each response before it is sent.</summary>
    public Func<Response, Response> Rewrite { get; set; } = response => response;

    public async ValueTask<TransferResult> Invoke(ITransferContext context)
    {
        var range = context.Http!.Headers.Select(line => line.Split(": ", 2)).FirstOrDefault(parts => parts[0] == "Range")?[1];
        Ranges.Add(range);
        var response = Rewrite(range is null ? new(200, [], representation) : Part(range["bytes=".Length..]));
        var head = Encoding.Latin1.GetBytes($"HTTP/1.1 {response.Code} X\r\n" + string.Concat(response.Fields.Select(field => field.Key + ": " + field.Value + "\r\n")) + "\r\n");
        await context.HeaderOutput!.WriteAsync(head);
        await context.Output.WriteAsync(response.Body);
        return response.Result ?? TransferResult.Success(response.Body.Length) with
        {
            Report = new TransferReport { ResponseCode = response.Code, ResponseHeaders = response.Fields },
        };
    }

    private Response Part(string spec)
    {
        var bounds = spec.Split('-').Select(int.Parse).ToArray();
        return new(206, [new("Content-Range", $"bytes {bounds[0]}-{bounds[1]}/{representation.Length}")], representation[bounds[0]..(bounds[1] + 1)]);
    }

    /// <summary>One response the origin sends.</summary>
    internal sealed record Response(int Code, KeyValuePair<string, string>[] Fields, byte[] Body, TransferResult? Result = null);
}
