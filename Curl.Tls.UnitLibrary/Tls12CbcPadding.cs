namespace Curl.Tls;

/// <summary>
/// TLS CBC padding (RFC 5246 section 6.2.3.2): <c>padding_length + 1</c> bytes, each
/// holding <c>padding_length</c>. <see cref="Check" /> reads it without a branch on the
/// decrypted bytes, as OpenSSL's <c>tls1_cbc_remove_padding_and_mac</c> does, so a bad
/// padding and a bad MAC take the same path to <c>bad_record_mac</c>.
/// </summary>
internal static class Tls12CbcPadding
{
    /// <summary>The longest padding a record can carry, <c>padding_length</c> being one byte.</summary>
    public const int MaximumPaddingLength = 255;

    /// <summary>Returns <paramref name="data" /> followed by the shortest padding that fills its last block.</summary>
    public static byte[] AppendPadding(ReadOnlySpan<byte> data, int blockSize)
    {
        int paddingLength = blockSize - 1 - (data.Length % blockSize);
        byte[] padded = new byte[data.Length + paddingLength + 1];
        data.CopyTo(padded);
        padded.AsSpan(data.Length).Fill((byte)paddingLength);
        return padded;
    }

    /// <summary>
    /// Checks the padding at the end of <paramref name="plaintext" />, which must be at
    /// least <paramref name="macLength" /> + 1 bytes, in time that depends only on its
    /// length: every one of the last 256 bytes is read, and the answer is a mask.
    /// </summary>
    /// <param name="plaintext">The decrypted record.</param>
    /// <param name="macLength">The MAC length that must fit before the padding; zero for encrypt-then-MAC.</param>
    /// <param name="unpaddedLength">The length without the padding when it is good; the whole length when it is not.</param>
    /// <returns>All ones when the padding is good; zero when it is not.</returns>
    public static uint Check(ReadOnlySpan<byte> plaintext, int macLength, out int unpaddedLength)
    {
        uint length = (uint)plaintext.Length;
        uint paddingLength = plaintext[^1];
        uint good = GreaterOrEqualMask(length, paddingLength + 1 + (uint)macLength);
        int bytesToCheck = Math.Min(MaximumPaddingLength + 1, plaintext.Length);
        for (int distanceFromEnd = 0; distanceFromEnd < bytesToCheck; distanceFromEnd++)
        {
            uint isPadding = GreaterOrEqualMask(paddingLength, (uint)distanceFromEnd);
            good &= ~(isPadding & NotEqualMask(plaintext[plaintext.Length - 1 - distanceFromEnd], paddingLength));
        }

        unpaddedLength = (int)(length - (good & (paddingLength + 1)));
        return good;
    }

    /// <summary>All ones when <paramref name="left" /> is at least <paramref name="right" />; both must be below 2^31.</summary>
    private static uint GreaterOrEqualMask(uint left, uint right) => ((left - right) >> 31) - 1;

    /// <summary>All ones when <paramref name="left" /> and <paramref name="right" /> differ.</summary>
    private static uint NotEqualMask(uint left, uint right)
    {
        uint difference = left ^ right;
        return (uint)((int)(difference | (0u - difference)) >> 31);
    }
}
