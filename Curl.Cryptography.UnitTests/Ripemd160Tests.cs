using System.Text;
using Curl.Testing;

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
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] hash = new byte[Ripemd160.HashSize];
        diagnostics.Arrange("vector source", "Bosselaers' RIPEMD-160 page, test vectors");
        diagnostics.Bytes("message", Encoding.ASCII.GetBytes(message));

        Ripemd160.HashData(Encoding.ASCII.GetBytes(message), hash);
        diagnostics.Act("hash", Convert.ToHexStringLower(hash));

        diagnostics.Diff("hash", Convert.FromHexString(expected), hash);
        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    // Bosselaers' RIPEMD-160 page: one million times "a".
    [TestMethod]
    public void AppendData_OneMillionA_GivesThePublishedHash()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] thousand = Enumerable.Repeat((byte)'a', 1000).ToArray();
        byte[] hash = new byte[Ripemd160.HashSize];
        using Ripemd160 ripemd160 = new();
        diagnostics.Arrange("vector source", "Bosselaers' RIPEMD-160 page, one million times \"a\"");
        diagnostics.Arrange("appends", "1000 of 1000 bytes of \"a\"");

        using (diagnostics.Phase("append one million bytes"))
        {
            for (int count = 0; count < 1000; count++)
            {
                ripemd160.AppendData(thousand);
            }
        }

        ripemd160.GetHashAndReset(hash);
        diagnostics.Act("hash", Convert.ToHexStringLower(hash));
        diagnostics.Diff("hash", Convert.FromHexString("52783243c1697bdbe16d37f97f68f08325dc1528"), hash);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("1234567890", 20)));
        byte[] whole = new byte[Ripemd160.HashSize];
        byte[] pieces = new byte[Ripemd160.HashSize];
        Ripemd160.HashData(message, whole);
        using Ripemd160 ripemd160 = new();
        diagnostics.Arrange("chunk length", chunkLength);
        diagnostics.Bytes("message", message);
        diagnostics.Arrange("whole-message hash", Convert.ToHexStringLower(whole));

        for (int offset = 0; offset < message.Length; offset += chunkLength)
        {
            ripemd160.AppendData(message.AsSpan(offset, Math.Min(chunkLength, message.Length - offset)));
        }

        ripemd160.GetHashAndReset(pieces);
        diagnostics.Act("chunked hash", Convert.ToHexStringLower(pieces));
        diagnostics.Diff("hash", whole, pieces);
        Assert.AreEqual(Convert.ToHexStringLower(whole), Convert.ToHexStringLower(pieces));
    }

    [TestMethod]
    public void GetHashAndReset_CalledTwice_StartsAnEmptyMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] hash = new byte[Ripemd160.HashSize];
        using Ripemd160 ripemd160 = new();
        ripemd160.AppendData("abc"u8);
        ripemd160.GetHashAndReset(hash);
        diagnostics.Arrange("first message", "abc");
        diagnostics.Arrange("first hash", Convert.ToHexStringLower(hash));

        ripemd160.GetHashAndReset(hash);
        diagnostics.Act("second hash", Convert.ToHexStringLower(hash));

        diagnostics.Diff("second hash (empty message)", Convert.FromHexString("9c1185a5c5e9fc54612808977ee8f548b2258d31"), hash);
        Assert.AreEqual("9c1185a5c5e9fc54612808977ee8f548b2258d31", Convert.ToHexStringLower(hash));
    }

    [TestMethod]
    [DataRow(19)]
    [DataRow(21)]
    public void GetHashAndReset_WrongDestinationLength_Throws(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Ripemd160 ripemd160 = new();
        diagnostics.Arrange("destination length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => ripemd160.GetHashAndReset(new byte[length]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void AppendData_AfterDispose_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Ripemd160 ripemd160 = new();
        ripemd160.Dispose();
        diagnostics.Arrange("state", "disposed");

        ObjectDisposedException exception = Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd160.AppendData("a"u8));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ObjectDisposedException), exception.GetType().Name);
    }

    [TestMethod]
    public void GetHashAndReset_AfterDispose_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Ripemd160 ripemd160 = new();
        ripemd160.Dispose();
        diagnostics.Arrange("state", "disposed");

        ObjectDisposedException exception = Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd160.GetHashAndReset(new byte[Ripemd160.HashSize]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ObjectDisposedException), exception.GetType().Name);
    }
}
