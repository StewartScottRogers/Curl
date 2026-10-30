using System.Security.Cryptography;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ContentDigest;

/// <summary>
/// The <c>--content-digest</c> transfer hook (ADR-1092). On the way out it asks for the server's digests
/// (<c>Want-Content-Digest</c>, <c>Want-Repr-Digest</c>, RFC 9530 §4) and gives a <c>-d</c> body's SHA-256
/// <c>Content-Digest</c> (§2) and <c>Repr-Digest</c> (§3), the same value since curl sends the body with no
/// content coding; a field <c>-H</c> already gave is left to it. On the way back it hashes the body as it is
/// written and fails the transfer with exit 56 when a <c>Content-Digest</c> or <c>Repr-Digest</c> member for
/// SHA-256 or SHA-512 does not match; other algorithms are ignored (§2). <c>Repr-Digest</c> is checked only
/// against a whole representation, not a 206, and neither is checked when curl decoded a content coding
/// (<c>--compressed</c>), or for a response with no content (<c>-I</c>, 304).
/// </summary>
internal sealed class ContentDigestTransferHook : ITransferHook
{
    private const string ContentDigest = "Content-Digest";

    private const string ReprDigest = "Repr-Digest";

    private const string WantedDigests = DigestAlgorithms.Sha256 + "=10, " + DigestAlgorithms.Sha512 + "=5";

    private const int PartialContent = 206;

    private const int NotModified = 304;

    public IEnumerable<string> RequestHeadersFor(ITransferContext context)
    {
        var http = context.Http!;
        List<KeyValuePair<string, string>> fields =
        [
            new("Want-" + ContentDigest, WantedDigests),
            new("Want-" + ReprDigest, WantedDigests),
        ];
        if (http.Body is BytesBody body)
        {
            var digest = DigestField.Format(DigestAlgorithms.Sha256, SHA256.HashData(body.Content.Span));
            fields.Add(new(ContentDigest, digest));
            fields.Add(new(ReprDigest, digest));
        }

        return fields.Where(field => !GivesField(http.Headers, field.Key)).Select(field => field.Key + ": " + field.Value);
    }

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        await using DigestingStream received = new(context.Output);
        var result = await next(context.WithOutputs(received, context.HeaderOutput, context.DumpHeaderOutput));
        return HasContentToCheck(context, result) && MismatchIn(result.Report!, received) is { } message
            ? TransferResult.Failure(CurlExitCode.RecvError, message, result.BytesTransferred) with { Report = result.Report }
            : result;
    }

    private static bool GivesField(IEnumerable<string> headers, string name) =>
        headers.Any(header => header.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase));

    private static bool HasContentToCheck(ITransferContext context, TransferResult result) =>
        result is { IsSuccess: true, Report: { ResponseCode: not NotModified } report }
        && !context.NoBody
        && !IsDecodedByCurl(context.Http!, report);

    private static bool IsDecodedByCurl(HttpRequestOptions http, TransferReport report) =>
        http.Compressed && IsContentCoded(report.ResponseHeaders);

    private static bool IsContentCoded(IReadOnlyList<KeyValuePair<string, string>> headers) =>
        FieldValueOf(headers, "Content-Encoding") is { } coding && !coding.Trim().Equals("identity", StringComparison.OrdinalIgnoreCase);

    private static string? MismatchIn(TransferReport report, DigestingStream received) =>
        MismatchOf(ContentDigest, report.ResponseHeaders, received)
        ?? (report.ResponseCode == PartialContent ? null : MismatchOf(ReprDigest, report.ResponseHeaders, received));

    private static string? MismatchOf(string field, IReadOnlyList<KeyValuePair<string, string>> headers, DigestingStream received)
    {
        foreach (var (algorithm, expected) in DigestField.Parse(FieldValueOf(headers, field)))
        {
            if (received.DigestOf(algorithm) is { } actual && !CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                return $"{field} {algorithm} does not match the response body";
            }
        }

        return null;
    }

    private static string? FieldValueOf(IReadOnlyList<KeyValuePair<string, string>> headers, string name)
    {
        var values = headers.Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value).ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }
}
