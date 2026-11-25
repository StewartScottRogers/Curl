using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Flows;
using Curl.Extension.OAuth2.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Options;

/// <summary>
/// Gets a token before the transfer, from the cache (refreshing it, RFC 6749 §6) or by the flow the options chose, and
/// sends it as <c>Authorization: Bearer</c> (RFC 6750 §2.1) after curl's own request headers (ADR-1075); with a key from
/// <see cref="TransferHookServices.DpopKeySource"/> (<c>--dpop</c>), gets a token bound to that key and sends it as
/// <c>Authorization: DPoP</c> (RFC 9449 §5, §7.1).
/// </summary>
/// <remarks>
/// A failed flow fails the transfer with the flow's exit code and message, and the transfer is never sent; so does a token
/// whose type is not the one asked for, <c>Bearer</c> or with a DPoP key <c>DPoP</c> (RFC 6749 §7.1, RFC 9449 §5), with
/// <see cref="CurlExitCode.WeirdServerReply"/>. This hook runs before <c>--dpop</c>'s, which therefore signs the request
/// carrying the token; a DPoP key that cannot be read is left to that hook to report, and no endpoint is asked. An HTTP
/// transfer that already carries a <c>DPoP</c> proof header (<c>-H</c>) fails with <see cref="CurlExitCode.FailedInit"/>
/// before any endpoint is asked: curl cannot give that proof the token's <c>ath</c> (RFC 9449 §4.2).
/// </remarks>
/// <param name="configuration">What the options ask for, or <see langword="null"/> when they are wrong.</param>
/// <param name="optionError">What is wrong with the options, when <paramref name="configuration"/> is <see langword="null"/>.</param>
/// <param name="messages">Where the flows' instructions for the user go.</param>
/// <param name="randomBytes">Gives cryptographically random octets.</param>
internal sealed class OAuth2TransferHook(OAuth2Configuration? configuration, string? optionError, TextWriter messages, Func<int, byte[]> randomBytes)
    : ITransferHook
{
    /// <summary>The failure when the host gives no connector.</summary>
    public const string NoServicesMessage = "The OAuth 2.0 options need curl's connector, which this transfer was not given.";

    /// <summary>The failure when the transfer carries a DPoP proof curl did not sign, which the flow's token cannot be bound to.</summary>
    public const string DpopProofMessage =
        "The --oauth2-* options cannot be combined with -H \"DPoP: ...\": curl cannot bind the flow's access token to a proof it did not sign. Use --dpop.";

    private const string DpopHeaderPrefix = "DPoP:";

    private string? firstHopAuthorization;

    /// <summary>Gets what the options ask for, or <see langword="null"/> when they are wrong.</summary>
    public OAuth2Configuration? Configuration => configuration;

    /// <inheritdoc/>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, NoServicesMessage));

    /// <inheritdoc/>
    public async ValueTask<TransferResult> ExecuteAsync(
        ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (configuration is null)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, optionError!);
        }

        if (SendsDpopProof(context))
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, DpopProofMessage);
        }

        if (TryHopWithoutAsking(context, out var hop))
        {
            return await next(hop);
        }

        if (services.DpopKeySource is not { } source)
        {
            return await AuthorizeAsync(context, services, null, next);
        }

        return await source.ReadDpopKeyAsync(context.CancellationToken) is { } key
            ? await AuthorizeAsync(context, services, key, next)
            : await next(context);
    }

    private static bool SendsDpopProof(ITransferContext context) => context.Http is { } http && http.Headers.Any(IsDpopProof);

    private static bool IsDpopProof(string header) =>
        header.StartsWith(DpopHeaderPrefix, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(header[DpopHeaderPrefix.Length..]);

    /// <summary>
    /// Gives the transfer for a followed hop without asking the token endpoint again: without the token when curl withholds its
    /// credentials there (another origin, no <c>--location-trusted</c>), with the first hop's token otherwise.
    /// </summary>
    private bool TryHopWithoutAsking(ITransferContext context, out ITransferContext hop)
    {
        hop = context;
        if (context.Http is not { RedirectsFollowed: > 0 } http)
        {
            return false;
        }

        if (!http.SendsCookieStrings)
        {
            return true;
        }

        if (Volatile.Read(ref firstHopAuthorization) is not { } kept)
        {
            return false;
        }

        hop = context.WithRequestHeaders([kept]);
        return true;
    }

    private async ValueTask<TransferResult> AuthorizeAsync(
        ITransferContext context, TransferHookServices services, IDpopKey? key, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var result = await GetTokenAsync(context, services, key);
        if (result.Token is not { } token)
        {
            return TransferResult.Failure(result.ExitCode, result.Message!);
        }

        var scheme = key is null ? "Bearer" : "DPoP";
        if (!string.Equals(token.TokenType, scheme, StringComparison.OrdinalIgnoreCase))
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, WrongTokenTypeMessage(token.TokenType, key));
        }

        var authorization = $"Authorization: {scheme} {token.AccessToken}";
        Volatile.Write(ref firstHopAuthorization, authorization);
        return await next(context.WithRequestHeaders([authorization]));
    }
    private async Task<OAuth2FlowResult> GetTokenAsync(ITransferContext context, TransferHookServices services, IDpopKey? key)
    {
        var request = configuration!.Request with { DpopKeyThumbprint = key?.Thumbprint };
        OAuth2HttpClient httpClient = new(services.Connector)
        {
            DpopProver = key is null ? null : new OAuth2DpopTokenProver(key, request.TokenEndpoint, context.TimeProvider),
        };
        var flow = configuration.FlowFor(httpClient, services, context.TimeProvider, messages, randomBytes);
        return configuration.CacheDirectory is { } directory
            ? await new OAuth2CachedTokenSource(new OAuth2TokenCache(new OAuth2TokenCacheFile(directory), httpClient, context.TimeProvider))
                .GetTokenAsync(request, flow, context.CancellationToken)
            : await flow.RunAsync(context.CancellationToken);
    }

    private static string WrongTokenTypeMessage(string tokenType, IDpopKey? key) =>
        key is null
            ? $"The token endpoint gave a token of type \"{tokenType}\", which curl cannot send."
            : $"The token endpoint gave a token of type \"{tokenType}\", not a DPoP token bound to the --dpop key.";
}
