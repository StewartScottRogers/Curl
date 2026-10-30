using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Dpop;

/// <summary>
/// The <c>--dpop</c> transfer hook (ADR-1145). Every HTTP request carries a fresh DPoP proof in a <c>DPoP</c> header
/// (RFC 9449 §4.1), signed with the key read once from the key file. An <c>--oauth2-bearer</c> token, or the token of a
/// <c>-H "Authorization: DPoP ..."</c>, is bound to the proof by <c>ath</c>, and <c>--oauth2-bearer</c>'s is sent as
/// <c>Authorization: DPoP</c> (§7.1). The last <c>DPoP-Nonce</c> each origin sent goes into its proofs, and a 400 or 401
/// answer that brings a new one is retried once with it (§8, §9); the response is held until then, so only the one
/// kept is written. A transfer that is not HTTP runs as it would without the hook.
/// </summary>
/// <param name="keyFile">The PEM file of the private key.</param>
internal sealed class DpopTransferHook(string keyFile) : ITransferHook
{
    private const string AuthorizationPrefix = "Authorization: DPoP ";

    private readonly ConcurrentDictionary<string, string> _noncesByOrigin = new(StringComparer.Ordinal);
    private DpopKey? _key;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        _key ??= await DpopKeyFile.ReadAsync(keyFile, context.CancellationToken);
        if (_key is null)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, $"Failed to read a DPoP private key from {keyFile}");
        }

        var attempt = await AttemptAsync(_key, context, next);
        if (attempt.AsksForNewNonce)
        {
            attempt = await AttemptAsync(_key, context, next);
        }

        await attempt.WriteToAsync(context);
        return attempt.Result;
    }

    /// <summary>Gives the <c>htu</c> of a URL: its scheme, host and port in lower case and its path, without query or fragment (RFC 9449 §4.2, §4.3).</summary>
    internal static string TargetUriOf(CurlUrl url) =>
        url.Scheme.ToLowerInvariant() + "://" + url.Host.ToLowerInvariant() + (url.IsDefaultPort ? string.Empty : ":" + url.Port) + url.AbsolutePath;
    /// <summary>Gives the method of the request curl's options build, as <c>htm</c> names it.</summary>
    internal static string MethodOf(ITransferContext context) =>
        context.Http!.CustomMethod
        ?? (context.NoBody ? "HEAD"
        : SendsBody(context) ? "POST"
        : context.Upload is not null ? "PUT"
        : "GET");

    /// <summary>Gives the access token the request sends: <c>--oauth2-bearer</c>'s, else that of a <c>-H "Authorization: DPoP ..."</c>.</summary>
    internal static string? AccessTokenOf(HttpRequestOptions http) =>
        http.BearerToken
        ?? http.Headers.Where(header => header.StartsWith(AuthorizationPrefix, StringComparison.OrdinalIgnoreCase)).Select(header => header[AuthorizationPrefix.Length..].Trim()).FirstOrDefault();

    private static bool SendsBody(ITransferContext context) =>
        context.PostData is not null || context.PostWithoutData || context.Http!.Body is not null;

    private async Task<DpopAttempt> AttemptAsync(DpopKey key, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var origin = context.Url.Scheme.ToLowerInvariant() + "://" + context.Url.Host.ToLowerInvariant() + ":" + context.Url.Port;
        var nonce = _noncesByOrigin.GetValueOrDefault(origin);
        var proof = DpopProof.Create(key, new DpopRequest(NewProofId(), MethodOf(context), TargetUriOf(context.Url), context.TimeProvider.GetUtcNow(), AccessTokenOf(context.Http!), nonce));
        var attempt = new DpopAttempt(context.DumpHeaderOutput is not null);
        attempt.Result = await next(context.WithRequestHeaders(HeadersFor(proof, context.Http!)).WithOutputs(attempt.Body, attempt.Head, attempt.DumpedHead));
        if (NonceGivenBy(attempt.Result) is { } given)
        {
            _noncesByOrigin[origin] = given;
            attempt.AsksForNewNonce = given != nonce && attempt.Result.Report!.ResponseCode is 400 or 401;
        }

        return attempt;
    }

    private static List<string> HeadersFor(string proof, HttpRequestOptions http)
    {
        List<string> headers = ["DPoP: " + proof];
        if (http.BearerToken is { } bearer)
        {
            headers.Add(AuthorizationPrefix + bearer);
        }

        return headers;
    }

    private static string? NonceGivenBy(TransferResult result) =>
        result.Report?.ResponseHeaders.FirstOrDefault(field => field.Key.Equals("DPoP-Nonce", StringComparison.OrdinalIgnoreCase)).Value;

    private static string NewProofId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}
