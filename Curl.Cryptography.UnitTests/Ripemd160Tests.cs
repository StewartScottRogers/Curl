using System.Text;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Ripemd160" /> to the reference vectors on the RIPEMD-160 page of
/// Bosselaers (homes.esat.kuleuven.be/~bosselae/ripemd160.html), checks that a message
/// split across <see cref="Ripemd160.AppendData" /> calls hashes as the whole, and checks
/// its caller mistakes (ADR-0118).
/// </summary>
[TestClass]
public sealed class Ripemd160Tests
{
    // Bosselaers' RIPEMD-160 page, "test vectors".
    [TestMethod]
    [DataRow("", "9c1185a5c5e9fc54612808977ee8f548b2258d31")]
    [DataRow("a", "0bdc9d2d256b3ee9daae347be6f4dc835a467ffe")]
    [DataRow("abc", "8eb208f7e05d987a9b044a8e98c6b087f15a0bfc")]
    [DataRow("message digest", "5d0689ef49d2fae572b881b123a85ffa21595f36")]
    [DataRow("abcdefghijklmnopqrstuvwxyz", "f71c27109c692c1b56bbdceb5b9d2865b3708dbc")]
    [DataRow("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "12a053384a9c0c88e405a06c27dcf49ada62eb2b")]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "b0e20b6e3116640286ed3a87a5713079b21f5189")]
    [DataRow("12345678901234567890123456789012345678901234567890123456789012345678901234567890", "9b752e45573d4b39f4dbd3323cab82bf63326bfb")]
    public void HashData_ReferenceVector_GivesThePublishedHash(string message, string expected)
    {
        byte[] hash = new byte[Ripemd160.HashSize];

        Ripemd160.HashData(Encoding.ASCII.GetBytes(message), hash);

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    // Bosselaers' RIPEMD-160 page: one million times "a".
    [TestMethod]
    public void AppendData_OneMillionA_GivesThePublishedHash()
    {
        byte[] thousand = Enumerable.Repeat((byte)'a', 1000).ToArray();
        byte[] hash = new byte[Ripemd160.HashSize];
        using Ripemd160 ripemd160 = new();

        for (int count = 0; count < 1000; count++)
        {
            ripemd160.AppendData(thousand);
        }

        ripemd160.GetHashAndReset(hash);
        Assert.AreEqual("52783243c1697bdbe16d37f97f68f08325dc1528", Convert.ToHexStringLower(hash));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(55)]
    [DataRow(56)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    public void AppendData_MessageSplitAcrossCalls_HashesAsTheWhole(int chunkLength)
    {
        byte[] message = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("1234567890", 20)));
        byte[] whole = new byte[Ripemd160.HashSize];
        byte[] pieces = new byte[Ripemd160.HashSize];
        Ripemd160.HashData(message, whole);
        using Ripemd160 ripemd160 = new();

        for (int offset = 0; offset < message.Length; offset += chunkLength)
        {
            ripemd160.AppendData(message.AsSpan(offset, Math.Min(chunkLength, message.Length - offset)));
        }

        ripemd160.GetHashAndReset(pieces);
        Assert.AreEqual(Convert.ToHexStringLower(whole), Convert.ToHexStringLower(pieces));
    }

    [TestMethod]
    public void GetHashAndReset_CalledTwice_StartsAnEmptyMessage()
    {
        byte[] hash = new byte[Ripemd160.HashSize];
        using Ripemd160 ripemd160 = new();
        ripemd160.AppendData("abc"u8);
        ripemd160.GetHashAndReset(hash);

        ripemd160.GetHashAndReset(hash);

        Assert.AreEqual("9c1185a5c5e9fc54612808977ee8f548b2258d31", Convert.ToHexStringLower(hash));
    }

    [TestMethod]
    [DataRow(19)]
    [DataRow(21)]
    public void GetHashAndReset_WrongDestinationLength_Throws(int length)
    {
        using Ripemd160 ripemd160 = new();

        Assert.ThrowsExactly<ArgumentException>(() => ripemd160.GetHashAndReset(new byte[length]));
    }

    [TestMethod]
    public void AppendData_AfterDispose_Throws()
    {
        Ripemd160 ripemd160 = new();
        ripemd160.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd160.AppendData("a"u8));
    }

    [TestMethod]
    public void GetHashAndReset_AfterDispose_Throws()
    {
        Ripemd160 ripemd160 = new();
        ripemd160.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd160.GetHashAndReset(new byte[Ripemd160.HashSize]));
    }
}
