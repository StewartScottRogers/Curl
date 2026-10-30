using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Makes a resumable upload's last response the transfer's: its body, its <c>-i</c> and <c>-D</c> headers, and its result.</summary>
internal static class ResumableUploadOutcome
{
    /// <summary>Writes <paramref name="response" /> to the transfer's outputs and gives the transfer's result.</summary>
    /// <param name="context">The transfer.</param>
    /// <param name="response">The last exchange.</param>
    /// <param name="uploadSize">The upload's length in bytes.</param>
    /// <returns>Success, or with <c>-f</c> and a status of 400 or more <see cref="CurlExitCode.HttpReturnedError" /> (22), as curl's handler gives it.</returns>
    public static async Task<TransferResult> WriteAsync(ITransferContext context, ResumableUploadExchange response, long uploadSize)
    {
        var fail = context.Http?.Fail ?? HttpFailMode.None;
        var failed = fail != HttpFailMode.None && response.StatusCode >= 400;
        var head = Encoding.Latin1.GetBytes(FormatHead(response));
        if (!(failed && fail == HttpFailMode.Fail))
        {
            await WriteHeadAsync(context.HeaderOutput, head, context.CancellationToken);
            await WriteHeadAsync(context.DumpHeaderOutput, head, context.CancellationToken);
            await context.Output.WriteAsync(response.Body, context.CancellationToken);
        }

        var report = new TransferReport
        {
            ResponseCode = response.StatusCode,
            HttpVersion = HttpVersion.Version11,
            Method = response.Method,
            ResponseHeaders = response.Headers,
            HeaderSize = head.Length,
            UploadSize = uploadSize,
            DownloadSize = response.Body.Length,
        };
        return failed
            ? TransferResult.Failure(CurlExitCode.HttpReturnedError, $"The requested URL returned error: {response.StatusCode}") with { Report = report }
            : TransferResult.Success(response.Body.Length) with { Report = report };
    }

    private static string FormatHead(ResumableUploadExchange response)
    {
        var head = new StringBuilder($"HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}\r\n");
        foreach (var (name, value) in response.Headers)
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        return head.Append("\r\n").ToString();
    }

    private static async Task WriteHeadAsync(Stream? output, byte[] head, CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await output.WriteAsync(head, cancellationToken);
        }
    }
}
