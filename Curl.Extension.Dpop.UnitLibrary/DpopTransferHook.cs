using System.Collections.Concurrent;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Dpop;

/// <summary>
/// The <c>--dpop</c> transfer hook (ADR-1145). Every HTTP request carries a fresh DPoP proof in a <c>DPoP</c> header
/// (RFC 9449 §4.1), signed with the key read once from the key file. An <c>--oauth2-bearer</c> token, or the token of a
/// <c>Authorization: DPoP</c> header (<c>-H</c>'s, or an outer hook's for a token it fetched with this hook's key), is
/// bound to the proof by <c>ath</c>, and <c>--oauth2-bearer</c>'s is sent as <c>Authorization: DPoP</c> (§7.1) unless
/// such a header already sends a token. The last <c>DPoP-Nonce</c> each origin sent goes into its proofs, and a 400 or 401
/// answer that brings a new one is retried once with it (§8, §9), its <c>-T</c> upload and its streamed request body
/// (<see cref="StreamBody" />) rewound first, and not retried when either cannot be rewound; the response is held until
/// then, so only the one kept is written. The hook runs inside every hook that sends several requests or changes a
/// request's method or URL (ADR-1426, ADR-1439), so each request is signed on its own; a <c>grpc</c> or <c>grpcs</c>
/// call's proof names the HTTP <c>POST</c> that carries it. A transfer that is not HTTP runs as it would without the
/// hook.
/// </summary>
/// <param name="keyFile">The PEM file of the private key.</param>
internal sealed class DpopTransferHook(string keyFile) : IDpopKeySource
{
    private const string AuthorizationPrefix = "Authorization: DPoP ";

    private readonly ConcurrentDictionary<string, string> _noncesByOrigin = new(StringComparer.Ordinal);
    private DpopKey? _key;

    public async ValueTask<IDpopKey?> ReadDpopKeyAsync(CancellationToken cancellationToken) =>
        await ReadKeyAsync(cancellationToken) is { } key ? new DpopPostProofKey(key) : null;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        if (await ReadKeyAsync(context.CancellationToken) is not { } key)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, $"Failed to read a DPoP private key from {keyFile}");
        }

        var body = (context.Http.Body as StreamBody)?.Content;
        var uploadStart = PositionOf(context.Upload);
        var bodyStart = PositionOf(body);
        var attempt = await AttemptAsync(key, context, next);
        if (attempt.AsksForNewNonce && TryRewind(context.Upload, uploadStart) && TryRewind(body, bodyStart))
        {
            attempt = await AttemptAsync(key, context, next);
        }

        await attempt.WriteToAsync(context);
        return attempt.Result;
    }

    /// <summary>
    /// Gives the <c>htu</c> of a URL: the scheme of the HTTP request, its host and port in lower case and its path, without
    /// query or fragment (RFC 9449 §4.2, §4.3). A <c>grpc</c> URL's request goes over <c>http</c>, a <c>grpcs</c> one's
    /// over <c>https</c>.
    /// </summary>
    internal static string TargetUriOf(CurlUrl url) =>
        HttpSchemeOf(url) + "://" + url.Host.ToLowerInvariant() + (url.IsDefaultPort ? string.Empty : ":" + url.Port) + url.AbsolutePath;

    /// <summary>
    /// Gives the method of the request, as <c>htm</c> names it: <c>POST</c> for a gRPC call, whatever the options say,
    /// otherwise that of the request curl's options build.
    /// </summary>
    internal static string MethodOf(ITransferContext context) =>
        IsGrpc(context.Url) ? "POST" : OptionsMethodOf(context);

    private static string OptionsMethodOf(ITransferContext context) =>
        context.Http!.CustomMethod
        ?? (context.NoBody ? "HEAD"
        : SendsBody(context) ? "POST"
        : context.Upload is not null ? "PUT"
        : "GET");

    private static bool IsGrpc(CurlUrl url) => url.Scheme.ToLowerInvariant() is "grpc" or "grpcs";

    private static string HttpSchemeOf(CurlUrl url) =>
        url.Scheme.ToLowerInvariant() switch
        {
            "grpc" => "http",
            "grpcs" => "https",
            var scheme => scheme,
        };

    /// <summary>
    /// Gives the access token the request sends: that of an <c>Authorization: DPoP ...</c> header, which curl sends in place
    /// of its own, else <c>--oauth2-bearer</c>'s.
    /// </summary>
    internal static string? AccessTokenOf(HttpRequestOptions http) => HeaderTokenOf(http) ?? http.BearerToken;

    private static long PositionOf(Stream? sent) => sent is { CanSeek: true } ? sent.Position : 0;

    /// <summary>Takes the upload or request body back to where the first try found it, so the retry sends it whole.</summary>
    /// <returns><see langword="false" /> when it cannot be taken back; the refusal then stands.</returns>
    private static bool TryRewind(Stream? sent, long start)
    {
        switch (sent)
        {
            case null:
                return true;
            case { CanSeek: true }:
                sent.Position = start;
                return true;
            case IUploadRewinder rewinder:
                return !rewinder.HasBeenRead || rewinder.RewindToStart() is null;
            default:
                return false;
        }
    }

    private static string? HeaderTokenOf(HttpRequestOptions http) =>
        http.Headers.Where(header => header.StartsWith(AuthorizationPrefix, StringComparison.OrdinalIgnoreCase)).Select(header => header[AuthorizationPrefix.Length..].Trim()).FirstOrDefault();

    private async ValueTask<DpopKey?> ReadKeyAsync(CancellationToken cancellationToken) =>
        _key ??= await DpopKeyFile.ReadAsync(keyFile, cancellationToken);

    private static bool SendsBody(ITransferContext context) =>
        context.PostData is not null || context.PostWithoutData || context.Http!.Body is not null;

    private async Task<DpopAttempt> AttemptAsync(DpopKey key, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var origin = context.Url.Scheme.ToLowerInvariant() + "://" + context.Url.Host.ToLowerInvariant() + ":" + context.Url.Port;
        var nonce = _noncesByOrigin.GetValueOrDefault(origin);
        var proof = DpopProof.Create(key, new DpopRequest(DpopProof.NewId(), MethodOf(context), TargetUriOf(context.Url), context.TimeProvider.GetUtcNow(), AccessTokenOf(context.Http!), nonce));
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
        if (http.BearerToken is { } bearer && HeaderTokenOf(http) is null)
        {
            headers.Add(AuthorizationPrefix + bearer);
        }

        return headers;
    }

    private static string? NonceGivenBy(TransferResult result) =>
        result.Report?.ResponseHeaders.FirstOrDefault(field => field.Key.Equals("DPoP-Nonce", StringComparison.OrdinalIgnoreCase)).Value;
}
