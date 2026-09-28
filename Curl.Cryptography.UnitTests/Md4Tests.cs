using System.Text;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Md4" /> to RFC 1320's test suite (appendix A.5), checks that a message
/// split across <see cref="Md4.AppendData" /> calls hashes as the whole, and checks its
/// caller mistakes (ADR-0118).
/// </summary>
[TestClass]
public sealed class Md4Tests
{
    // RFC 1320 appendix A.5, all seven.
    [TestMethod]
    [DataRow("", "31d6cfe0d16ae931b73c59d7e0c089c0")]
    [DataRow("a", "bde52cb31de33e46245e05fbdbd6fb24")]
    [DataRow("abc", "a448017aaf21d8525fc10ae87aa6729d")]
    [DataRow("message digest", "d9130a8164549fe818874806e1c7014b")]
    [DataRow("abcdefghijklmnopqrstuvwxyz", "d79e1c308aa5bbcdeea8ed63df412da9")]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "043f8582f241db351ce627e153e7f0e4")]
    [DataRow("12345678901234567890123456789012345678901234567890123456789012345678901234567890", "e33b4ddc9c38f2199c3e7b164fcc0536")]
    public void HashData_Rfc1320Vector_GivesThePublishedHash(string message, string expected)
    {
        byte[] hash = new byte[Md4.HashSize];

        Md4.HashData(Encoding.ASCII.GetBytes(message), hash);

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
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
        byte[] whole = new byte[Md4.HashSize];
        byte[] pieces = new byte[Md4.HashSize];
        Md4.HashData(message, whole);
        using Md4 md4 = new();

        for (int offset = 0; offset < message.Length; offset += chunkLength)
        {
            md4.AppendData(message.AsSpan(offset, Math.Min(chunkLength, message.Length - offset)));
        }

        md4.GetHashAndReset(pieces);
        Assert.AreEqual(Convert.ToHexStringLower(whole), Convert.ToHexStringLower(pieces));
    }

    [TestMethod]
    public void GetHashAndReset_CalledTwice_StartsAnEmptyMessage()
    {
        byte[] hash = new byte[Md4.HashSize];
        using Md4 md4 = new();
        md4.AppendData("abc"u8);
        md4.GetHashAndReset(hash);

        md4.GetHashAndReset(hash);

        Assert.AreEqual("31d6cfe0d16ae931b73c59d7e0c089c0", Convert.ToHexStringLower(hash));
    }

    [TestMethod]
    [DataRow(15)]
    [DataRow(17)]
    public void GetHashAndReset_WrongDestinationLength_Throws(int length)
    {
        using Md4 md4 = new();

        Assert.ThrowsExactly<ArgumentException>(() => md4.GetHashAndReset(new byte[length]));
    }

    [TestMethod]
    public void AppendData_AfterDispose_Throws()
    {
        Md4 md4 = new();
        md4.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => md4.AppendData("a"u8));
    }

    [TestMethod]
    public void GetHashAndReset_AfterDispose_Throws()
    {
        Md4 md4 = new();
        md4.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => md4.GetHashAndReset(new byte[Md4.HashSize]));
    }
}
