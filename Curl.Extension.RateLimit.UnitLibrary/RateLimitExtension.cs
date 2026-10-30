using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RateLimit;

/// <summary>
/// The <c>--ratelimit-pace</c> extension (ADR-1106): RateLimit header fields (draft-ietf-httpapi-ratelimit-headers-11).
/// It reads each HTTP response's <c>RateLimit</c> and <c>RateLimit-Policy</c> fields and, when the server says a quota
/// is used up, holds every later transfer to that origin in the option group (<c>--retry</c> attempts and <c>-Z</c>
/// parallel transfers alike) until the quota resets. <c>Curl.Extension.Composition</c> registers its option,
/// description and transfer hook.
/// </summary>
public static class RateLimitExtension
{
    /// <summary>Gets the <c>--ratelimit-pace</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "ratelimit-pace",
        ExtensionOptionArgument.None,
        "Wait before the next request to an origin whose RateLimit field says its quota is used up, until the quota resets");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "ratelimit-pace",
        "Reads RateLimit and RateLimit-Policy response fields and waits, before retries and later or parallel transfers to the same origin, until a used-up quota resets (draft-ietf-httpapi-ratelimit-headers-11).",
        [],
        [Option],
        new Uri("https://datatracker.ietf.org/doc/draft-ietf-httpapi-ratelimit-headers/"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--ratelimit-pace</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, shared by the group's transfers, or <see langword="null" /> when <c>--ratelimit-pace</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new RateLimitTransferHook() : null;
    }
}
