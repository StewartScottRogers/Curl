using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Turns a file name the harness read one character per byte (a <c>&lt;client&gt;&lt;file name&gt;</c>
/// or <c>&lt;verify&gt;&lt;file name&gt;</c>) into the name the file system gives the file.
/// <c>runtests.pl</c> passes the case's bytes on as they are, and the file system reads them as
/// UTF-8, so <c>%hex[%E6%BC%A2]hex%</c> names a file <c>漢</c>, not the three characters U+00E6
/// U+00BC U+00A2 (test3203, test1703).
/// </summary>
internal static class UpstreamUtf8Words
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The word's bytes read as UTF-8, or the word as it is when they are not valid UTF-8.</summary>
    /// <param name="word">The word, one character per byte.</param>
    /// <returns>The word as the operating system reads it.</returns>
    public static string AsSystemText(string word)
    {
        try
        {
            return StrictUtf8.GetString(Encoding.Latin1.GetBytes(word));
        }
        catch (DecoderFallbackException)
        {
            return word;
        }
    }
}
