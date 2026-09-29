using System.Text;

namespace Curl.Zstandard;

/// <summary>
/// Pins <see cref="XxHash64" /> and <see cref="XxHash64Accumulator" /> to published XXH64
/// values: xxHash's own sanity table (<c>xxhsum</c>'s test buffer, seeds 0 and
/// <c>PRIME32_1</c>) and the python-xxhash README's strings (ADR-0185).
/// </summary>
[TestClass]
public sealed class XxHash64Tests
{
    /// <summary>xxhsum's non-zero sanity seed, <c>XXH_PRIME32_1</c>.</summary>
    internal const ulong SanitySeed = 2654435761;

    // xxhsum's sanity table: the first N bytes of its generated test buffer.
    [TestMethod]
    [DataRow(0, 0UL, 0xEF46DB3751D8E999UL)]
    [DataRow(0, SanitySeed, 0xAC75FDA2929B17EFUL)]
    [DataRow(1, 0UL, 0xE934A84ADB052768UL)]
    [DataRow(1, SanitySeed, 0x5014607643A9B4C3UL)]
    [DataRow(4, 0UL, 0x9136A0DCA57457EEUL)]
    [DataRow(14, 0UL, 0x8282DCC4994E35C8UL)]
    [DataRow(14, SanitySeed, 0xC3BD6BF63DEB6DF0UL)]
    [DataRow(222, 0UL, 0xB641AE8CB691C174UL)]
    [DataRow(222, SanitySeed, 0x20CB8AB7AE10C14AUL)]
    public void Hash_XxhsumSanityBuffer_MatchesPublishedValue(int length, ulong seed, ulong expected)
    {
        var data = SanityBuffer(length);

        Assert.AreEqual(expected, XxHash64.Hash(data, seed));
        Assert.AreEqual(expected, HashOneByteAtATime(data, seed));
    }

    // python-xxhash's README.
    [TestMethod]
    [DataRow("xxhash", 0UL, 0x32DD38952C4BC720UL)]
    [DataRow("xxhash", 20141025UL, 0xB559B98D844E0635UL)]
    [DataRow("Nobody inspects the spammish repetition", 0UL, 0xFBCEA83C8A378BF1UL)]
    public void Hash_PythonXxhashReadmeString_MatchesPublishedValue(string text, ulong seed, ulong expected)
    {
        var data = Encoding.ASCII.GetBytes(text);

        Assert.AreEqual(expected, XxHash64.Hash(data, seed));
        Assert.AreEqual(expected, HashOneByteAtATime(data, seed));
    }

    [TestMethod]
    public void Hash_NoSeed_UsesSeedZero()
    {
        Assert.AreEqual(0xEF46DB3751D8E999UL, XxHash64.Hash([]));
    }

    private static ulong HashOneByteAtATime(byte[] data, ulong seed)
    {
        var accumulator = new XxHash64Accumulator(seed);
        foreach (var value in data)
        {
            accumulator.Append([value]);
        }

        return accumulator.GetCurrentHash();
    }

    /// <summary>xxhsum's <c>BMK_fillTestBuffer</c>: each byte is the top byte of a generator multiplied by xxhsum's <c>PRIME64</c> (11400714785074694797) in turn.</summary>
    internal static byte[] SanityBuffer(int length)
    {
        var buffer = new byte[length];
        ulong generator = SanitySeed;
        for (var index = 0; index < length; index++)
        {
            buffer[index] = (byte)(generator >> 56);
            generator *= 11400714785074694797;
        }

        return buffer;
    }
}
