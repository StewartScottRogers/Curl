using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// ADR-0142's hand-built NTLM route (N): curl 8.21.0's own NTLM, from <c>Curl.Ntlm</c>. The
/// first step writes curl's fixed NEGOTIATE (Type 1) message; the second reads the server's
/// CHALLENGE (Type 2) message and answers it with the AUTHENTICATE (Type 3) message
/// <paramref name="answerer" /> builds for the request's credential, which completes the
/// context.
/// </summary>
/// <param name="request">
/// The credential: <c>DOMAIN\user</c> when it has a domain, and an empty user and password
/// for the default credentials, as curl sends them.
/// </param>
/// <param name="answerer">Builds the AUTHENTICATE message, with its client challenge and time.</param>
internal sealed class HandBuiltNtlmSecurityContext(SecurityContextRequest request, NtlmChallengeAnswerer answerer) : ISecurityContext
{
    private bool negotiated;

    /// <inheritdoc />
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets which of curl's two buffer checks refused the AUTHENTICATE message:
    /// <see cref="NtlmMessageFailure.None" /> until one does, then
    /// <see cref="NtlmMessageFailure.ResponsesTooLarge" /> or
    /// <see cref="NtlmMessageFailure.NamesTooLarge" />, so the authenticator can fail with the
    /// message curl prints for that check.
    /// </summary>
    public NtlmMessageFailure AnswerRefusedBecause { get; private set; }

    /// <inheritdoc />
    public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Step(incomingToken.Span));
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: curl's own NTLM keeps no session key, so it gives no message protection.</exception>
    public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw NoMessageProtection();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: curl's own NTLM keeps no session key, so it gives no message protection.</exception>
    public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw NoMessageProtection();

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private static NotSupportedException NoMessageProtection() =>
        new("curl's own NTLM gives no message protection; wrapping needs the system NTLM.");

    private SecurityContextStep Step(ReadOnlySpan<byte> incomingToken)
    {
        if (!negotiated)
        {
            negotiated = true;
            return new SecurityContextStep(SecurityContextStatus.ContinueNeeded, NtlmNegotiateMessage.Encode());
        }

        return IsCompleted ? new SecurityContextStep(SecurityContextStatus.Refused, []) : Answer(incomingToken);
    }

    /// <summary>
    /// Answers the CHALLENGE message: <see cref="SecurityContextStatus.MalformedToken" /> when
    /// it cannot be read, <see cref="SecurityContextStatus.Refused" /> when the answer passes
    /// curl's 1024-byte buffer, keeping which check refused it in <see cref="AnswerRefusedBecause" />.
    /// </summary>
    private SecurityContextStep Answer(ReadOnlySpan<byte> incomingToken)
    {
        if (NtlmChallengeMessage.Decode(incomingToken).Message is not { } challenge)
        {
            return new SecurityContextStep(SecurityContextStatus.MalformedToken, []);
        }

        if (!answerer.Answer(challenge, CurlUserName(), request.Password ?? string.Empty).TryEncode(out byte[]? message, out NtlmMessageFailure failure))
        {
            AnswerRefusedBecause = failure;
            return new SecurityContextStep(SecurityContextStatus.Refused, []);
        }

        IsCompleted = true;
        return new SecurityContextStep(SecurityContextStatus.Completed, message);
    }

    /// <summary>Gets the user as curl's <c>-u</c> holds it, which <see cref="NtlmUserName.SplitDomain" /> splits again.</summary>
    private string CurlUserName() =>
        request.Domain is null ? request.UserName ?? string.Empty : request.Domain + "\\" + request.UserName;
}
