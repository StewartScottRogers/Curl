using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// The device authorization grant (RFC 8628): asks for a device code (§3.1), tells the user where to enter the user
/// code (§3.3), and polls the token endpoint (§3.4) until the user signs in, refuses, or the code expires (§3.5).
/// </summary>
/// <remarks>
/// Polls every <c>interval</c> seconds by the <see cref="TimeProvider"/>, at least one, adding
/// <see cref="SlowDownIncrease"/> on each <c>slow_down</c>. It stops with <see cref="CurlExitCode.OperationTimedOut"/>
/// once <c>expires_in</c> has passed or on <c>expired_token</c>, and with <see cref="CurlExitCode.LoginDenied"/> on
/// <c>access_denied</c> or any other error (ADR-1045).
/// </remarks>
/// <param name="httpClient">Sends the device authorization and token requests.</param>
/// <param name="timeProvider">Waits between polls and tells when the device code expires.</param>
/// <param name="messages">Where the instructions for the user are written: curl's standard error.</param>
/// <param name="request">The token endpoint, client and scope.</param>
/// <param name="deviceAuthorizationEndpoint">The device authorization endpoint (§3.1).</param>
public sealed class OAuth2DeviceAuthorizationFlow(
    OAuth2HttpClient httpClient,
    TimeProvider timeProvider,
    TextWriter messages,
    OAuth2FlowRequest request,
    Uri deviceAuthorizationEndpoint) : IOAuth2TokenFlow
{
    /// <summary>How much longer the client waits between polls after each <c>slow_down</c> (§3.5).</summary>
    public static readonly TimeSpan SlowDownIncrease = TimeSpan.FromSeconds(5);

    private const string DeviceAuthorizationEndpointName = "device authorization endpoint";

    // Caps interval at a day and expires_in at 100 years so an absurd value cannot overflow TimeSpan or DateTimeOffset.
    private const long LongestIntervalSeconds = 24 * 60 * 60;

    private const long LongestLifetimeSeconds = 100L * 366 * 24 * 60 * 60;

    /// <inheritdoc/>
    public async Task<OAuth2FlowResult> RunAsync(CancellationToken cancellationToken)
    {
        var (authorization, failure) = await OAuth2TokenEndpoint.PostAsync(
            httpClient,
            deviceAuthorizationEndpoint,
            OAuth2TokenRequestWriter.WriteDeviceAuthorization(request.Client, request.Scope),
            OAuth2DeviceAuthorizationResponseParser.Parse,
            DeviceAuthorizationEndpointName,
            cancellationToken);
        if (authorization is null)
        {
            return failure!;
        }

        await messages.WriteAsync(DescribeInstructions(authorization).AsMemory(), cancellationToken);
        await messages.FlushAsync(cancellationToken);
        return await PollAsync(authorization, cancellationToken);
    }

    /// <summary>What the user is told (§3.3): the verification URI and user code, and the complete URI when given (§3.3.1).</summary>
    /// <param name="authorization">The device authorization response.</param>
    /// <returns>The lines, each ending in a newline.</returns>
    public static string DescribeInstructions(OAuth2DeviceAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var instructions = $"To sign in, open {authorization.VerificationUri} and enter the code {authorization.UserCode}\n";
        return authorization.VerificationUriComplete is { } complete
            ? instructions + $"or open {complete}, which carries the code.\n"
            : instructions;
    }

    private async Task<OAuth2FlowResult> PollAsync(OAuth2DeviceAuthorization authorization, CancellationToken cancellationToken)
    {
        var expiresIn = Math.Clamp(authorization.ExpiresIn, 0, LongestLifetimeSeconds);
        var expiresAt = timeProvider.GetUtcNow().AddSeconds(expiresIn);
        var interval = TimeSpan.FromSeconds(Math.Clamp(authorization.Interval, 1, LongestIntervalSeconds));
        var form = OAuth2TokenRequestWriter.WriteDeviceCode(request.Client, authorization.DeviceCode);
        while (true)
        {
            await Task.Delay(interval, timeProvider, cancellationToken);
            if (timeProvider.GetUtcNow() >= expiresAt)
            {
                return OAuth2FlowResult.Failed(
                    CurlExitCode.OperationTimedOut, $"The device code expired after {expiresIn} seconds before the user signed in.");
            }

            var result = await OAuth2TokenEndpoint.RequestTokenAsync(httpClient, request.TokenEndpoint, form, cancellationToken);
            switch (result.Error?.Code)
            {
                case OAuth2ErrorCode.AuthorizationPending:
                    break;
                case OAuth2ErrorCode.SlowDown:
                    interval += SlowDownIncrease;
                    break;
                default:
                    return result;
            }
        }
    }
}
