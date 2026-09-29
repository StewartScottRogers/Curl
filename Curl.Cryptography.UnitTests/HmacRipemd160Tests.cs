using System.Text;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="HmacRipemd160" /> to RFC 2286 section 2's seven test cases, checks that
/// a message split across <see cref="HmacRipemd160.AppendData" /> calls, and a second
/// message on the same instance, give the same MACs, and checks
/// <see cref="HmacRipemd160.Verify" /> rejects a flipped bit (ADR-0118).
/// </summary>
[TestClass]
public sealed class HmacRipemd160Tests
{
    private const string Hi = "4869205468657265";

    // RFC 2286 section 2, test cases 1 to 7 (key, data and MAC in hexadecimal); case 5's
    // data is "Test With Truncation", its full 160-bit MAC.
    [TestMethod]
    [DataRow("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", Hi, "24cb4bd67d20fc1a5d2ed7732dcc39377f0a5668")]
    [DataRow("4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", "dda6c0213a485a9e24f4742064a7f033b43c4069")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "dd*50", "b0b105360de759960ab4f35298e116e295d8e7c1")]
    [DataRow("0102030405060708090a0b0c0d0e0f10111213141516171819", "cd*50", "d5ca862f4d21d5e610e18b4cf1beb97a4365ecf4")]
    [DataRow("0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c", "546573742057697468205472756e636174696f6e", "7619693978f91d90539ae786500ff3d8e0518e39")]
    [DataRow("aa*80", "54657374205573696e67204c6172676572205468616e20426c6f636b2d53697a65204b6579202d2048617368204b6579204669727374", "6466ca07ac5eac29e1bd523e5ada7605b791fd8b")]
    [DataRow("aa*80", "54657374205573696e67204c6172676572205468616e20426c6f636b2d53697a65204b657920616e64204c6172676572205468616e204f6e6520426c6f636b2d53697a652044617461", "69ea60798d71616cce5fd0871e23754cd75d5a0a")]
    public void HashData_Rfc2286Vector_GivesThePublishedMac(string key, string data, string expected)
    {
        byte[] mac = new byte[HmacRipemd160.HashSize];

        HmacRipemd160.HashData(FromHex(key), FromHex(data), mac);

        Assert.AreEqual(expected, Convert.ToHexStringLower(mac));
    }

    [TestMethod]
    public void AppendData_MessageSplitAcrossCalls_MacsAsTheWhole()
    {
        byte[] message = Encoding.ASCII.GetBytes("Test Using Larger Than Block-Size Key and Larger Than One Block-Size Data");
        byte[] mac = new byte[HmacRipemd160.HashSize];
        using HmacRipemd160 hmac = new(FromHex("aa*80"));

        for (int offset = 0; offset < message.Length; offset += 5)
        {
            hmac.AppendData(message.AsSpan(offset, Math.Min(5, message.Length - offset)));
        }

        hmac.GetHashAndReset(mac);
        Assert.AreEqual("69ea60798d71616cce5fd0871e23754cd75d5a0a", Convert.ToHexStringLower(mac));
    }

    [TestMethod]
    public void GetHashAndReset_SecondMessage_IsMacedUnderTheSameKey()
    {
        byte[] mac = new byte[HmacRipemd160.HashSize];
        using HmacRipemd160 hmac = new(FromHex("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b"));
        hmac.AppendData("something else"u8);
        hmac.GetHashAndReset(mac);

        hmac.AppendData(FromHex(Hi));
        hmac.GetHashAndReset(mac);

        Assert.AreEqual("24cb4bd67d20fc1a5d2ed7732dcc39377f0a5668", Convert.ToHexStringLower(mac));
    }

    [TestMethod]
    public void Verify_PublishedMac_ReturnsTrue()
    {
        bool verified = HmacRipemd160.Verify("Jefe"u8, "what do ya want for nothing?"u8, FromHex("dda6c0213a485a9e24f4742064a7f033b43c4069"));

        Assert.IsTrue(verified);
    }

    [TestMethod]
    public void Verify_FlippedBit_ReturnsFalse()
    {
        bool verified = HmacRipemd160.Verify("Jefe"u8, "what do ya want for nothing?"u8, FromHex("dda6c0213a485a9e24f4742064a7f033b43c4068"));

        Assert.IsFalse(verified);
    }

    [TestMethod]
    [DataRow(12)]
    [DataRow(21)]
    public void Verify_WrongMacLength_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => HmacRipemd160.Verify("Jefe"u8, "x"u8, new byte[length]));
    }

    [TestMethod]
    [DataRow(19)]
    [DataRow(21)]
    public void GetHashAndReset_WrongDestinationLength_Throws(int length)
    {
        using HmacRipemd160 hmac = new("Jefe"u8);

        Assert.ThrowsExactly<ArgumentException>(() => hmac.GetHashAndReset(new byte[length]));
    }

    [TestMethod]
    public void AppendData_AfterDispose_Throws()
    {
        HmacRipemd160 hmac = new("Jefe"u8);
        hmac.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => hmac.AppendData("a"u8));
    }

    [TestMethod]
    public void GetHashAndReset_AfterDispose_Throws()
    {
        HmacRipemd160 hmac = new("Jefe"u8);
        hmac.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => hmac.GetHashAndReset(new byte[HmacRipemd160.HashSize]));
    }

    /// <summary>Hexadecimal, or <c>xx*n</c> for the byte <c>xx</c> repeated n times as RFC 2286 writes it.</summary>
    private static byte[] FromHex(string text)
    {
        string[] parts = text.Split('*');
        return parts.Length == 1
            ? Convert.FromHexString(text)
            : Enumerable.Repeat(Convert.FromHexString(parts[0])[0], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }
}
