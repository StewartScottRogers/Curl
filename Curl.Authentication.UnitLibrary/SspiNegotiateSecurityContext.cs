using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// SSPI's Negotiate as curl 8.21.0 gets it (ADR-0176): a first token that falls back to NTLM,
/// which the BCL's <c>NegotiateAuthentication</c> makes off a domain, answers
/// <see cref="SecurityContextStatus.NoCredentials" /> instead, because curl measured on the
/// same SSPI gets <c>SEC_E_NO_CREDENTIALS</c> there, with <c>-u :</c> and <c>-u u:p</c> alike,
/// and sends nothing. A Kerberos token passes through unchanged, as does every later step.
/// </summary>
/// <param name="sspi">The SSPI Negotiate context.</param>
internal sealed class SspiNegotiateSecurityContext(ISecurityContext sspi) : ISecurityContext
{
    private bool firstStepTaken;

    /// <inheritdoc />
    public bool IsCompleted => sspi.IsCompleted;

    /// <inheritdoc />
    public async ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
    {
        bool isFirstStep = !firstStepTaken;
        firstStepTaken = true;
        SecurityContextStep step = await sspi.NextTokenAsync(incomingToken, cancellationToken).ConfigureAwait(false);
        return isFirstStep && NtlmInNegotiate.Carries(step.Token)
            ? new SecurityContextStep(SecurityContextStatus.NoCredentials, [])
            : step;
    }

    /// <inheritdoc />
    public void Dispose() => sspi.Dispose();
}
