namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Decodes base64 as libssh2 1.11.1's <c>_libssh2_base64_decode</c> does, which is how it
/// reads the salt and hash of a hashed known-hosts name: every character outside the
/// base64 alphabet, <c>=</c> included, is skipped, and only a lone leftover character
/// (a count of alphabet characters one more than a multiple of four) is an error.
/// </summary>
internal static class Libssh2Base64
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>
    /// Decodes <paramref name="text" /> leniently.
    /// </summary>
    /// <param name="text">The base64 text.</param>
    /// <param name="bytes">The decoded bytes, or <see langword="null" /> when the text is invalid.</param>
    /// <returns><see langword="true" /> when it decoded.</returns>
    internal static bool TryDecode(string text, out byte[]? bytes)
    {
        int[] values = [.. text.Select(character => Alphabet.IndexOf(character, StringComparison.Ordinal)).Where(value => value >= 0)];
        if (values.Length % 4 == 1)
        {
            bytes = null;
            return false;
        }

        int bits = 0;
        int bitCount = 0;
        List<byte> decoded = [];
        foreach (int value in values)
        {
            bits = ((bits << 6) | value) & 0xFFFF;
            bitCount += 6;
            if (bitCount >= 8)
            {
                bitCount -= 8;
                decoded.Add((byte)(bits >> bitCount));
            }
        }

        bytes = [.. decoded];
        return true;
    }
}
