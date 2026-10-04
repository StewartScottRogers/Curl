namespace Curl.Networking;

/// <summary>
/// curl 8.21.0's refusal to resolve a <c>.onion</c> name (RFC 7686): <c>Curl_resolv</c> in
/// <c>lib/hostip.c</c> fails any name of at least 7 characters that ends in <c>.onion</c> or
/// <c>.onion.</c>, in any letter case, before its DNS cache or resolver is asked, with
/// <c>Not resolving .onion address (RFC 7686)</c> (measured, BL-1394).
/// </summary>
internal static class OnionAddress
{
    /// <summary>The message curl writes, and fails the transfer with, for a <c>.onion</c> name.</summary>
    public const string RefusalMessage = "Not resolving .onion address (RFC 7686)";

    /// <summary>Says whether curl refuses to resolve <paramref name="host" /> as a <c>.onion</c> name.</summary>
    /// <param name="host">The name about to be resolved.</param>
    /// <returns><see langword="true" /> for <c>x.onion</c> or <c>x.onion.</c>; <see langword="false" /> for <c>onion</c>, <c>.onion</c> or <c>x.onion.example</c>.</returns>
    public static bool IsRefused(string host) =>
        host.Length >= 7
        && (host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".onion.", StringComparison.OrdinalIgnoreCase));
}
