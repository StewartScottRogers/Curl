using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Har;

/// <summary>
/// The <c>--har &lt;file&gt;</c> transfer hook (ADR-1146). It copies each HTTP transfer's response body on its
/// way to curl's output, and when the transfer ends adds an entry to the archive (HAR 1.2, "entries") and
/// writes the whole archive to the file again, so the file always holds every transfer of the option group so
/// far. A transfer that is not HTTP passes through unrecorded. A file that cannot be written fails the
/// transfer with exit 23, as curl fails one whose output cannot be written.
/// </summary>
/// <param name="path">The file the archive is written to.</param>
internal sealed class HarTransferHook(string path) : ITransferHook
{
    private readonly List<HarExchange> _exchanges = [];

    private readonly SemaphoreSlim _writing = new(1, 1);

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        var started = context.TimeProvider.GetUtcNow();
        await using BodyCopyingStream received = new(context.Output);
        var result = await next(context.WithOutputs(received, context.HeaderOutput, context.DumpHeaderOutput));
        HarExchange exchange = new(
            started,
            MethodOf(context, result.Report),
            result.Report?.EffectiveUrl ?? context.Url.OriginalString,
            RequestHeadersOf(context.Http),
            context.Http.Body as BytesBody,
            result,
            received.Copied(),
            context.TimeProvider.TimestampFrequency);
        return await RecordAsync(exchange, context.CancellationToken) ? result : WriteFailure(result);
    }

    private static string MethodOf(ITransferContext context, TransferReport? report) =>
        report?.Method ?? context.Http!.CustomMethod ?? DefaultMethodOf(context);

    private static string DefaultMethodOf(ITransferContext context) => context switch
    {
        { NoBody: true } => "HEAD",
        { Upload: not null } => "PUT",
        { Http.Body: not null } => "POST",
        _ => "GET",
    };

    private static List<KeyValuePair<string, string>> RequestHeadersOf(HttpRequestOptions http)
    {
        var given = http.Headers.Select(FieldOf).OfType<KeyValuePair<string, string>>().ToList();
        List<KeyValuePair<string, string>> headers = [];
        AddUnlessGiven(headers, given, "User-Agent", http.UserAgent);
        AddUnlessGiven(headers, given, "Referer", http.Referer);
        headers.AddRange(given);
        return headers;
    }

    private static void AddUnlessGiven(List<KeyValuePair<string, string>> headers, List<KeyValuePair<string, string>> given, string name, string? value)
    {
        if (value is not null && !given.Any(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(new(name, value));
        }
    }

    /// <summary>Reads a <c>-H</c> line as the field it sends: none for <c>Name:</c>, which removes a field, or <c>Name;</c>.</summary>
    private static KeyValuePair<string, string>? FieldOf(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var value = colon < 0 ? string.Empty : line[(colon + 1)..].Trim();
        return value.Length == 0 ? null : new(line[..colon].Trim(), value);
    }

    private async Task<bool> RecordAsync(HarExchange exchange, CancellationToken cancellationToken)
    {
        await _writing.WaitAsync(cancellationToken);
        try
        {
            _exchanges.Add(exchange);
            await File.WriteAllBytesAsync(path, HarDocument.Write(_exchanges), cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            _writing.Release();
        }
    }

    private TransferResult WriteFailure(TransferResult result) =>
        TransferResult.Failure(CurlExitCode.WriteError, $"Failed writing the HAR file {path}", result.BytesTransferred) with { Report = result.Report };
}
