using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Stands in for the rest of curl's transfer: records each request it is given and answers from a script.</summary>
/// <param name="answers">One answer per request, in order.</param>
internal sealed class ScriptedNext(params ScriptedNext.Answer[] answers)
{
    private int next;

    /// <summary>Gets the requests given so far.</summary>
    public List<SentRequest> Sent { get; } = [];

    /// <summary>Gives a response with <paramref name="status" />, <paramref name="headers" /> and <paramref name="body" />.</summary>
    public static Answer Response(int status, string body = "", params string[] headers) => new(status, headers, body, null, null);

    /// <summary>Gives a failed transfer.</summary>
    public static Answer Failure(CurlExitCode exitCode, string? message = "broken") => new(0, [], string.Empty, exitCode, message);

    /// <summary>Records <paramref name="context" /> and answers it.</summary>
    public async ValueTask<TransferResult> SendAsync(ITransferContext context)
    {
        string? upload = null;
        if (context.Upload is { } stream)
        {
            var read = new MemoryStream();
            await stream.CopyToAsync(read);
            upload = Encoding.ASCII.GetString(read.ToArray());
        }

        Sent.Add(new(context.Http?.CustomMethod ?? (context.NoBody ? "HEAD" : "GET"), context.Url.OriginalString, context.Http?.Headers ?? [], upload, context));
        var answer = answers[next++];
        if (answer.ExitCode is { } exitCode)
        {
            return answer.Message is null ? new TransferResult(exitCode, 0) : TransferResult.Failure(exitCode, answer.Message);
        }

        var fields = answer.Headers.Select(line => new KeyValuePair<string, string>(line[..line.IndexOf(':')], line[(line.IndexOf(':') + 2)..])).ToList();
        await context.HeaderOutput!.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {answer.Status} R\r\n{string.Concat(answer.Headers.Select(line => line + "\r\n"))}\r\n"));
        await context.Output.WriteAsync(Encoding.ASCII.GetBytes(answer.Body));
        return answer.Status == 0
            ? TransferResult.Success(answer.Body.Length)
            : TransferResult.Success(answer.Body.Length) with { Report = new TransferReport { ResponseCode = answer.Status, ResponseHeaders = fields } };
    }

    /// <summary>One scripted answer; a status of 0 gives a result without a report.</summary>
    internal sealed record Answer(int Status, string[] Headers, string Body, CurlExitCode? ExitCode, string? Message);

    /// <summary>One request the hook sent.</summary>
    internal sealed record SentRequest(string Method, string Url, IReadOnlyList<string> Headers, string? Upload, ITransferContext Context);
}
