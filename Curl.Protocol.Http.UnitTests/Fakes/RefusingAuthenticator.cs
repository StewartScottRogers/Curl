using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that refuses every request with one
/// <see cref="HttpAuthenticationFailedException" />, as <c>--aws-sigv4</c> refuses a request it
/// cannot sign, and counts its calls.
/// </summary>
/// <param name="exitCode">The exit code the refusal fails the transfer with.</param>
/// <param name="message">The refusal's message.</param>
public sealed class RefusingAuthenticator(CurlExitCode exitCode, string message) : IHttpAuthenticator
{
    /// <summary>Gets how many times the authenticator was asked.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        Calls++;
        throw new HttpAuthenticationFailedException(exitCode, message);
    }
}
