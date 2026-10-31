using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Tokens;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Options;

/// <summary>
/// Runs the transfer and, when it is answered 401 with a <c>PrivateToken</c> challenge of token type <c>0x0002</c>
/// (RFC 9577 §2.1), gets a token for it from the issuer (<see cref="PrivacyPassIssuance"/>) and runs the transfer again
/// with <c>Authorization: PrivateToken</c> (RFC 9577 §2.2) after curl's own request headers (ADR-1110).
/// </summary>
/// <remarks>
/// The first response is held back, so the 401 that is answered never reaches the output; any other first response,
/// including a 401 with no challenge this extension can answer, is written out as curl received it.
/// </remarks>
/// <param name="issuerUrl">The issuer's request URL or origin (discovered through its directory, RFC 9578 §4), or <see langword="null"/> when <c>--privacy-pass</c>'s value is not an absolute URL.</param>
/// <param name="randomBytes">Gives cryptographically random octets.</param>
internal sealed class PrivacyPassTransferHook(Uri? issuerUrl, Func<int, byte[]> randomBytes) : ITransferHook
{
    /// <summary>The failure when the host gives no connector.</summary>
    public const string NoServicesMessage = "--privacy-pass needs curl's connector, which this transfer was not given.";

    /// <summary>The failure when <c>--privacy-pass</c>'s value is not an absolute URL.</summary>
    public const string NotAUrlMessage = "--privacy-pass needs the issuer's absolute request URL or origin.";

    /// <inheritdoc/>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, NoServicesMessage));

    /// <inheritdoc/>
    public async ValueTask<TransferResult> ExecuteAsync(
        ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(next);
        if (issuerUrl is null)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, NotAUrlMessage);
        }

        using var body = new MemoryStream();
        using var headers = new MemoryStream();
        using var dumpedHeaders = new MemoryStream();
        var first = await next(context.WithOutputs(body, context.HeaderOutput is null ? null : headers, context.DumpHeaderOutput is null ? null : dumpedHeaders));
        if (AnswerableChallenge(first) is not { } challenge)
        {
            await WriteHeldBackAsync(body, context.Output, context);
            await WriteHeldBackAsync(headers, context.HeaderOutput, context);
            await WriteHeldBackAsync(dumpedHeaders, context.DumpHeaderOutput, context);
            return first;
        }

        try
        {
            var client = new PrivacyPassHttpClient(services.Connector);
            var requestUrl = await IssuerRequestUrlAsync(client, issuerUrl, challenge, context.CancellationToken);
            var issuance = new PrivacyPassIssuance(client, requestUrl, randomBytes);
            var authorization = await issuance.RedeemAsync(challenge, context.CancellationToken);
            return await next(context.WithRequestHeaders([$"Authorization: {authorization}"]));
        }
        catch (PrivacyPassException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>Finds the first <c>PrivateToken</c> challenge of token type <c>0x0002</c> in a 401's <c>WWW-Authenticate</c> fields.</summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The challenge, or <see langword="null"/> when the response is not a 401 or has none.</returns>
    internal static PrivateTokenChallengeHeader? AnswerableChallenge(TransferResult result) =>
        result.Report is { ResponseCode: 401 } report
            ? report.ResponseHeaders
                .Where(field => string.Equals(field.Key, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
                .SelectMany(field => PrivateTokenChallengeHeader.FindAll(field.Value))
                .FirstOrDefault(header => header.Challenge.TokenType == PrivacyPassIssuance.BlindRsaTokenType)
            : null;

    // An issuer origin is discovered through its directory (RFC 9578 §4), which must list the challenge's key; any other URL is the request URL.
    private static async Task<Uri> IssuerRequestUrlAsync(
        PrivacyPassHttpClient client, Uri url, PrivateTokenChallengeHeader challenge, CancellationToken cancellationToken)
    {
        if (!PrivacyPassIssuerDirectory.IsIssuerOrigin(url))
        {
            return url;
        }

        var directory = await PrivacyPassIssuerDirectory.FetchAsync(client, url, cancellationToken);
        return directory.Lists(challenge.TokenKey)
            ? directory.RequestUrl
            : throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The PrivateToken challenge's token-key is not one the issuer directory lists.");
    }

    private static async Task WriteHeldBackAsync(MemoryStream held, Stream? output, ITransferContext context)
    {
        if (output is not null)
        {
            await output.WriteAsync(held.ToArray(), context.CancellationToken);
        }
    }
}
