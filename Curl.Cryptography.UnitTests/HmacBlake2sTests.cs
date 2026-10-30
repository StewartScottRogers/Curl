namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="HmacBlake2s" /> to RFC 2104's definition, H((K' xor opad) || H((K' xor
/// ipad) || m)) with H the unkeyed 32-byte <see cref="Blake2s" /> (itself pinned to its
/// published vectors) and a 64-byte block, for keys shorter than, equal to and longer than
/// a block. No body publishes HMAC-BLAKE2s vectors.
/// </summary>
[TestClass]
public sealed class HmacBlake2sTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(32)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(200)]
    public void HashData_AnyKeyLength_MatchesRfc2104OverBlake2s(int keyLength)
    {
        byte[] key = new byte[keyLength];
        for (int index = 0; index < keyLength; index++)
        {
            key[index] = (byte)(index * 7);
        }

        byte[] message = "WireGuard v1 zx2c4 Jason@zx2c4.com"u8.ToArray();
        byte[] mac = new byte[HmacBlake2s.HashSize];

        HmacBlake2s.HashData(key, message, mac);

        CollectionAssert.AreEqual(ComputeByDefinition(key, message), mac);
    }

    [TestMethod]
    public void HashData_DifferentKeys_GiveDifferentMacs()
    {
        byte[] first = new byte[HmacBlake2s.HashSize];
        byte[] second = new byte[HmacBlake2s.HashSize];

        HmacBlake2s.HashData([1], "message"u8, first);
        HmacBlake2s.HashData([2], "message"u8, second);

        CollectionAssert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void HashData_DestinationWrongLength_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => HmacBlake2s.HashData([1], [2], new byte[16]));

    private static byte[] ComputeByDefinition(byte[] key, byte[] message)
    {
        byte[] blockKey = new byte[64];
        if (key.Length > 64)
        {
            Blake2s.HashData(key, blockKey.AsSpan(0, 32));
        }
        else
        {
            key.CopyTo(blockKey, 0);
        }

        byte[] inner = new byte[32];
        Blake2s.HashData([.. blockKey.Select(b => (byte)(b ^ 0x36)), .. message], inner);
        byte[] outer = new byte[32];
        Blake2s.HashData([.. blockKey.Select(b => (byte)(b ^ 0x5c)), .. inner], outer);
        return outer;
    }
}
