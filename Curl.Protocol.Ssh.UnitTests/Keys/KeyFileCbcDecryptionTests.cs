using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="KeyFileCbcDecryption" />'s hand-built DES-CBC against the vectors it
/// checks by construction, and every length and padding it refuses.
/// </summary>
[TestClass]
public sealed class KeyFileCbcDecryptionTests
{
    private static readonly byte[] Key = Convert.FromHexString("0123456789ABCDEF");

    private static readonly byte[] Iv = Convert.FromHexString("1122334455667788");

    [TestMethod]
    [DataRow("Aes128", 16)]
    [DataRow("Aes192", 24)]
    [DataRow("Aes256", 32)]
    [DataRow("TripleDes", 24)]
    [DataRow("Des", 8)]
    public void KeyLength_EachCipher(string cipher, int length)
    {
        Assert.AreEqual(length, KeyFileCbcDecryption.KeyLength(Enum.Parse<KeyFileCipher>(cipher)));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "empty plaintext: a whole block of padding")]
    [DataRow(5, DisplayName = "part of a block")]
    [DataRow(16, DisplayName = "two blocks")]
    public void Decrypt_Des_UndoesCbcAndPadding(int length)
    {
        byte[] plain = [.. Enumerable.Range(0, length).Select(value => (byte)value)];

        byte[] decrypted = KeyFileCbcDecryption.Decrypt(KeyFileCipher.Des, Key, Iv, EncryptDes(plain, padding: null));

        CollectionAssert.AreEqual(plain, decrypted);
    }

    [TestMethod]
    [DataRow(7, 0, DisplayName = "ciphertext not whole blocks")]
    [DataRow(0, 0, DisplayName = "empty ciphertext")]
    [DataRow(8, 4, DisplayName = "IV of four bytes")]
    public void Decrypt_DesLengthsWrong_Throws(int dataLength, int ivLength)
    {
        byte[] iv = ivLength == 0 ? Iv : new byte[ivLength];

        Assert.ThrowsExactly<CryptographicException>(() => KeyFileCbcDecryption.Decrypt(KeyFileCipher.Des, Key, iv, new byte[dataLength]));
    }

    [TestMethod]
    [DataRow((byte)0, DisplayName = "padding byte zero")]
    [DataRow((byte)9, DisplayName = "padding longer than a block")]
    [DataRow((byte)3, DisplayName = "padding bytes that differ")]
    public void Decrypt_DesPaddingInvalid_Throws(byte lastByte)
    {
        byte[] block = [1, 2, 3, 4, 5, 6, 7, lastByte];

        Assert.ThrowsExactly<CryptographicException>(() => KeyFileCbcDecryption.Decrypt(KeyFileCipher.Des, Key, Iv, EncryptDes(block, padding: false)));
    }

    // DES-CBC by hand with the same primitive, and PKCS #7 padding unless told not to pad.
    private static byte[] EncryptDes(byte[] plain, bool? padding)
    {
        int pad = padding is false ? 0 : Des.BlockSize - (plain.Length % Des.BlockSize);
        byte[] padded = [.. plain, .. Enumerable.Repeat((byte)pad, pad)];
        using Des des = new(Key);
        byte[] cipher = new byte[padded.Length];
        byte[] previous = Iv;
        for (int offset = 0; offset < padded.Length; offset += Des.BlockSize)
        {
            byte[] block = new byte[Des.BlockSize];
            for (int index = 0; index < Des.BlockSize; index++)
            {
                block[index] = (byte)(padded[offset + index] ^ previous[index]);
            }

            des.EncryptBlock(block, cipher.AsSpan(offset, Des.BlockSize));
            previous = cipher[offset..(offset + Des.BlockSize)];
        }

        return cipher;
    }
}
