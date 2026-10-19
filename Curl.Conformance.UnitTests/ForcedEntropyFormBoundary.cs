namespace Curl.Conformance;

/// <summary>
/// The MIME boundary of upstream's debug build under <c>CURL_ENTROPY</c> (lib/mime.c's <c>curl_mime_init</c> through
/// lib/rand.c's <c>Curl_rand_alnum</c>), which upstream test1972 sets so its signed MIME post is the same every run:
/// 24 dashes, then 22 characters, each the alphanumeric at one forced value's remainder by 62, the values following
/// <see cref="ForcedEntropyWebSocketRandomSource" />'s sequence from its first value (BL-2695).
/// </summary>
internal static class ForcedEntropyFormBoundary
{
    private const string Alphanumerics = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private const uint AlphanumericCount = 62;

    // Curl_rand_alnum draws again for a value at or past the largest multiple of 62, so every character is as likely.
    private const uint RejectedFrom = uint.MaxValue - (uint.MaxValue % AlphanumericCount);

    /// <summary>Gives the boundary the debug build makes under <paramref name="entropy" />.</summary>
    /// <param name="entropy">The value of <c>CURL_ENTROPY</c>.</param>
    /// <returns>24 dashes and 22 alphanumerics.</returns>
    internal static string Of(string entropy)
    {
        uint value = ForcedEntropyWebSocketRandomSource.FirstValue(entropy);
        char[] characters = new char[22];
        for (int index = 0; index < characters.Length; index++, value++)
        {
            while (value >= RejectedFrom)
            {
                value++;
            }

            characters[index] = Alphanumerics[(int)(value % AlphanumericCount)];
        }

        return new string('-', 24) + new string(characters);
    }
}
