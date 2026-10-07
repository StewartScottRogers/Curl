using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that reports scripted <c>-v</c> lines to the request's
/// events, as the <c>--aws-sigv4</c> signer reports its string to sign, and then gives one
/// scripted value for every call.
/// </summary>
/// <param name="value">The value every call gives.</param>
/// <param name="lines">The lines every call reports, in order.</param>
public sealed class LineReportingAuthenticator(string? value, params string[] lines) : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (string line in lines)
        {
            request.Events.ReportInfo(line);
        }

        return value;
    }
}
