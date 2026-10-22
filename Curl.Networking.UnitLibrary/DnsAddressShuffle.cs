using System.Security.Cryptography;

namespace Curl.Networking;

/// <summary>
/// Shuffles a host's resolved addresses for <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>, as curl 8.22.0's
/// <c>dns_shuffle_addr</c> (<c>lib/vdns/dnscache.c</c>) does: a Fisher-Yates shuffle from the last address down,
/// each swapped with one at a random index at or below it.
/// </summary>
internal static class DnsAddressShuffle
{
    /// <summary>
    /// A random index below the bound, from the cryptographic generator, as <c>Curl_rand</c> is.
    /// </summary>
    internal static Func<int, int> CryptographicRandomBelow { get; } = RandomNumberGenerator.GetInt32;

    /// <summary>Returns <paramref name="addresses" /> in a shuffled order, leaving the list itself unchanged.</summary>
    /// <typeparam name="T">The kind of address.</typeparam>
    /// <param name="addresses">The addresses in the order they resolved.</param>
    /// <param name="randomBelow">Gives a random index from zero up to, not including, its argument.</param>
    /// <returns>A new array of the same addresses, shuffled.</returns>
    internal static T[] Shuffle<T>(IReadOnlyList<T> addresses, Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(randomBelow);
        T[] shuffled = [.. addresses];
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = randomBelow(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
