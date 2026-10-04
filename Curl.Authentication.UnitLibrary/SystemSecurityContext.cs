using System.Buffers;
using System.ComponentModel;
using System.Net.Security;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// An <see cref="ISecurityContext" /> over the BCL's <see cref="NegotiateAuthentication" />:
/// SSPI on Windows, the system GSS-API library elsewhere (ADR-0142's W and G). The BCL
/// context is made on the first step, because SSPI acquires the credential while it is
/// made and can refuse it there; every step is synchronous underneath. Once established it
/// wraps and unwraps with the BCL's own <c>Wrap</c> and <c>Unwrap</c> (ADR-0183).
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
    public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt)
    {
        ArrayBufferWriter<byte> output = new();
        return WrittenOrNull(Established().Wrap(message, output, encrypt, out _), output);
    }

    /// <inheritdoc />
    public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage)
    {
        ArrayBufferWriter<byte> output = new();
        return WrittenOrNull(Established().Unwrap(wrappedMessage, output, out _), output);
    }

    /// <inheritdoc />
    public void Dispose() => authentication?.Dispose();

    private static byte[]? WrittenOrNull(NegotiateAuthenticationStatusCode code, ArrayBufferWriter<byte> output) =>
        code == NegotiateAuthenticationStatusCode.Completed ? output.WrittenSpan.ToArray() : null;

    private NegotiateAuthentication Established() =>
        IsCompleted ? authentication! : throw new InvalidOperationException("The security context is not established.");

    /// <summary>Gets the step the BCL's answer to one <c>GetOutgoingBlob</c> comes to.</summary>
    /// <param name="code">The status the BCL answered.</param>
    /// <param name="token">The token the BCL answered, <see langword="null" /> when it has none.</param>
    /// <returns>
    /// The mapped status, carrying <paramref name="token" /> (empty for none) only when the
    /// handshake goes on or completes, and an empty token for any failure.
    /// </returns>
    internal static SecurityContextStep StepOf(NegotiateAuthenticationStatusCode code, byte[]? token)
    {
        SecurityContextStatus status = NegotiateAuthenticationStatusMapping.StatusOf(code);
        return new SecurityContextStep(status, status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed ? token ?? [] : []);
    }

    private SecurityContextStep Step(ReadOnlySpan<byte> incomingToken)
    {
        try
        {
            authentication ??= new NegotiateAuthentication(options);
            byte[]? token = authentication.GetOutgoingBlob(incomingToken, out NegotiateAuthenticationStatusCode code);
            return StepOf(code, token);
        }
        catch (Win32Exception)
        {
            // SSPI's Kerberos package refuses an explicit credential off a domain with "The
            // logon attempt failed" while acquiring it, rather than answering a status (ADR-0142).
            return new SecurityContextStep(SecurityContextStatus.NoCredentials, []);
        }
    }
}
