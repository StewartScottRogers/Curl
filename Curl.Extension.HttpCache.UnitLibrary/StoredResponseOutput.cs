using System.Text;

namespace Curl.Extension.HttpCache;

/// <summary>
/// Writes a stored response to a transfer's outputs as curl's handler would have written it: the status
/// line and header fields to the <c>-i</c> and <c>-D</c> outputs, the body to the body output.
/// </summary>
internal static class StoredResponseOutput
{
    /// <summary>Writes <paramref name="stored" /> to <paramref name="context" />'s outputs.</summary>
    /// <returns>The number of body bytes written.</returns>
    public static async Task<long> WriteAsync(StoredResponse stored, ITransferContext context)
    {
        var head = Encoding.Latin1.GetBytes(
            stored.StatusLine + "\r\n" + string.Concat(stored.Headers.Select(field => field.Key + ": " + field.Value + "\r\n")) + "\r\n");
        await WriteIfGivenAsync(context.HeaderOutput, head, context.CancellationToken);
        await WriteIfGivenAsync(context.DumpHeaderOutput, head, context.CancellationToken);
        await context.Output.WriteAsync(stored.Body, context.CancellationToken);
        return stored.Body.Length;
    }

    /// <summary>Writes <paramref name="bytes" /> to <paramref name="output" /> when there is one.</summary>
    public static async Task WriteIfGivenAsync(Stream? output, byte[] bytes, CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await output.WriteAsync(bytes, cancellationToken);
        }
    }

    /// <summary>
    /// The status line of the last response in the header bytes curl's handler wrote, such as
    /// <c>HTTP/1.1 200 OK</c>; <c>HTTP/1.1</c> and <paramref name="statusCode" /> when they hold none.
    /// </summary>
    public static string StatusLineIn(byte[] headerBytes, int statusCode) =>
        Encoding.Latin1.GetString(headerBytes)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .LastOrDefault(line => line.StartsWith("HTTP/", StringComparison.Ordinal))
        ?? "HTTP/1.1 " + statusCode;
}
