using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Nats;

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

    private static bool IsValid(string subject) =>
        !subject.Any(char.IsWhiteSpace) && !subject.Split('.').Any(token => token.Length == 0);
}
