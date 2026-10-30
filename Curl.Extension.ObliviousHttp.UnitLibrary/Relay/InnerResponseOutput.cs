using System.Text;
using Curl.Extension.ObliviousHttp.BinaryHttp;

namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>
/// Writes the response an Oblivious HTTP response carried as curl writes an HTTP response: the content to the
/// body output, and to <c>-i</c>'s and <c>-D</c>'s outputs an HTTP/1.1 status line with an empty reason phrase
/// (Binary HTTP carries none, RFC 9292 section 3.5.2), the header fields and a blank line.
/// </summary>
internal static class InnerResponseOutput
{
    /// <summary>Writes <paramref name="response" /> to <paramref name="context" />'s outputs and gives the transfer's result.</summary>
    /// <param name="response">The decapsulated response.</param>
    /// <param name="method">The encapsulated request's method, which the result reports.</param>
    /// <param name="context">The transfer.</param>
    public static async Task<TransferResult> WriteAsync(BinaryHttpResponse response, string method, ITransferContext context)
    {
        byte[] head = Encoding.Latin1.GetBytes($"HTTP/1.1 {response.StatusCode} \r\n" + string.Concat(response.Headers.Select(field => $"{field.Name}: {field.Value}\r\n")) + "\r\n");
        await WriteIfGivenAsync(context.HeaderOutput, head, context.CancellationToken);
        await WriteIfGivenAsync(context.DumpHeaderOutput, head, context.CancellationToken);
        await context.Output.WriteAsync(response.Content, context.CancellationToken);
        IReadOnlyList<KeyValuePair<string, string>> headers = [.. response.Headers.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))];
        return TransferResult.Success(response.Content.Length) with
        {
            Report = new TransferReport
            {
                ResponseCode = response.StatusCode,
                ResponseHeaders = headers,
                Method = method,
                EffectiveUrl = context.Url.OriginalString,
                ContentType = headers.FirstOrDefault(field => string.Equals(field.Key, "content-type", StringComparison.OrdinalIgnoreCase)).Value,
            },
        };
    }

    private static async Task WriteIfGivenAsync(Stream? output, byte[] bytes, CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await output.WriteAsync(bytes, cancellationToken);
        }
    }
}
