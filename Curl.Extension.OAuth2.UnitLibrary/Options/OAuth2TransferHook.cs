using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Flows;
using Curl.Extension.OAuth2.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Options;

/// <summary>
/// Gets a token before the transfer, from the cache (refreshing it, RFC 6749 §6) or by the flow the options chose, and
/// sends it as <c>Authorization: Bearer</c> (RFC 6750 §2.1) after curl's own request headers (ADR-1075).
/// </summary>
/// <remarks>
/// A failed flow fails the transfer with the flow's exit code and message, and the transfer is never sent; so does a token
/// whose type is not Bearer, which RFC 6749 §7.1 forbids a client to use, with <see cref="CurlExitCode.WeirdServerReply"/>.
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

    private string? firstHopToken;

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

        if (TryHopWithoutAsking(context, out var hop))
        {
            return await next(hop);
        }

        var (accessToken, failure) = await GetAccessTokenAsync(context, services);
        if (accessToken is null)
        {
            return failure!;
        }

        Volatile.Write(ref firstHopToken, accessToken);
        return await next(WithBearer(context, accessToken));
    }

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

        if (Volatile.Read(ref firstHopToken) is not { } kept)
        {
            return false;
        }

        hop = WithBearer(context, kept);
        return true;
    }
    private async ValueTask<(string? AccessToken, TransferResult? Failure)> GetAccessTokenAsync(ITransferContext context, TransferHookServices services)
    {
        OAuth2HttpClient httpClient = new(services.Connector);
        var flow = configuration!.FlowFor(httpClient, services, context.TimeProvider, messages, randomBytes);
        var result = configuration.CacheDirectory is { } directory
            ? await new OAuth2CachedTokenSource(new OAuth2TokenCache(new OAuth2TokenCacheFile(directory), httpClient, context.TimeProvider))
                .GetTokenAsync(configuration.Request, flow, context.CancellationToken)
            : await flow.RunAsync(context.CancellationToken);
        if (result.Token is not { } token)
        {
            return (null, TransferResult.Failure(result.ExitCode, result.Message!));
        }

        return string.Equals(token.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? (token.AccessToken, null)
            : (null, TransferResult.Failure(
                CurlExitCode.WeirdServerReply, $"The token endpoint gave a token of type \"{token.TokenType}\", which curl cannot send."));
    }

    private static ITransferContext WithBearer(ITransferContext context, string accessToken) =>
        context.WithRequestHeaders([$"Authorization: Bearer {accessToken}"]);
}
