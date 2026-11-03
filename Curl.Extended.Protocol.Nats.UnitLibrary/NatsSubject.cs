using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>Gives the subject a NATS URL's path names (NATS client protocol, "Protocol conventions").</summary>
internal static class NatsSubject
{
    /// <summary>
    /// Gives the URL's percent-decoded path without its leading slash: <c>nats://host/a.b</c> names <c>a.b</c>.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>The subject.</returns>
    /// <exception cref="NatsException">The path is empty, or the subject holds whitespace or an empty token.</exception>
    public static string Of(CurlUrl url)
    {
        string subject = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/'));
        if (subject.Length == 0)
        {
            throw new NatsException(CurlExitCode.UrlMalformat, NatsMessages.NoSubject);
        }

        return IsValid(subject)
            ? subject
            : throw new NatsException(CurlExitCode.UrlMalformat, $"NATS subject \"{subject}\" holds whitespace or an empty token");
    }

    /// <summary>
    /// Refuses a subject that holds a wildcard token, <c>*</c> or <c>&gt;</c>, for a publish or a request:
    /// the NATS client protocol does not allow wildcards in a PUB subject ("Protocol conventions").
    /// </summary>
    /// <param name="subject">The subject the transfer would publish to.</param>
    /// <exception cref="NatsException">The subject holds a wildcard token.</exception>
    public static void RefuseWildcardToPublish(string subject)
    {
        if (subject.Split('.').Any(token => token is "*" or ">"))
        {
            throw new NatsException(CurlExitCode.UrlMalformat, $"NATS subject \"{subject}\" holds a wildcard, which a publish cannot name");
        }
    }

    private static bool IsValid(string subject) =>
        !subject.Any(char.IsWhiteSpace) && !subject.Split('.').Any(token => token.Length == 0);
}
