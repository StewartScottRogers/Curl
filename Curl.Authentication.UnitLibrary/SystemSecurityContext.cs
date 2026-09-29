using System.ComponentModel;
using System.Net.Security;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// An <see cref="ISecurityContext" /> over the BCL's <see cref="NegotiateAuthentication" />:
/// SSPI on Windows, the system GSS-API library elsewhere (ADR-0142's W and G). The BCL
/// context is made on the first step, because SSPI acquires the credential while it is
/// made and can refuse it there; every step is synchronous underneath.
/// </summary>
/// <param name="options">The package, target and credential.</param>
internal sealed class SystemSecurityContext(NegotiateAuthenticationClientOptions options) : ISecurityContext
{
    private NegotiateAuthentication? authentication;

    /// <inheritdoc />
    public bool IsCompleted => authentication?.IsAuthenticated == true;

    /// <inheritdoc />
    public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Step(incomingToken.Span));
    }

    /// <inheritdoc />
    public void Dispose() => authentication?.Dispose();

    private SecurityContextStep Step(ReadOnlySpan<byte> incomingToken)
    {
        try
        {
            authentication ??= new NegotiateAuthentication(options);
            byte[]? token = authentication.GetOutgoingBlob(incomingToken, out NegotiateAuthenticationStatusCode code);
            SecurityContextStatus status = NegotiateAuthenticationStatusMapping.StatusOf(code);
            return new SecurityContextStep(status, status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed ? token ?? [] : []);
        }
        catch (Win32Exception)
        {
            // SSPI's Kerberos package refuses an explicit credential off a domain with "The
            // logon attempt failed" while acquiring it, rather than answering a status (ADR-0142).
            return new SecurityContextStep(SecurityContextStatus.NoCredentials, []);
        }
    }
}
