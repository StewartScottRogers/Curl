using Curl.Testing;

namespace Curl.Zstandard;

/// <summary>
/// Attacks <see cref="XxHash64" /> and <see cref="XxHash64Accumulator" /> by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1524): every length around the 4-, 8-
/// and 32-byte boundaries, the extreme seeds, input appended in random pieces, and an
/// accumulator read, reset and appended to in orders the decoder never uses. The oracle is
/// the accumulator's documented contract: it gives what <see cref="XxHash64.Hash" /> gives
/// for all the input at once.
/// </summary>
[TestClass]
public sealed class XxHash64AdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0UL)]
    [DataRow(1UL)]
    [DataRow(ulong.MaxValue)]
    public void Append_EveryLengthUpTo130InRandomPieces_GivesWhatHashGives(ulong seed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const int randomSeed = 1524;
        diagnostics.Arrange("hash seed", seed);
        diagnostics.Arrange("random seed", randomSeed);
        var random = new Random(randomSeed);

        var mismatches = new List<int>();
        for (var length = 0; length <= 130; length++)
        {
            var data = new byte[length];
            random.NextBytes(data);
            var accumulator = new XxHash64Accumulator(seed);
            for (var position = 0; position < length;)
            {
                var piece = Math.Min(random.Next(0, 40), length - position);
                accumulator.Append(data.AsSpan(position, piece));
                position += piece;
            }

            if (accumulator.GetCurrentHash() != XxHash64.Hash(data, seed))
            {
                mismatches.Add(length);
            }
        }

        diagnostics.Act("lengths whose pieces hashed differently", string.Join(", ", mismatches));
        diagnostics.Assert("lengths whose pieces hashed differently", string.Empty, string.Join(", ", mismatches));
        Assert.IsEmpty(mismatches);
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(31)]
    [DataRow(32)]
    [DataRow(33)]
    public void GetCurrentHash_CalledTwiceThenAppendedTo_DoesNotDisturbTheHash(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var data = Enumerable.Range(0, length * 2).Select(index => (byte)(index * 7)).ToArray();
        diagnostics.Arrange("length of each half", length);
        var accumulator = new XxHash64Accumulator();

        accumulator.Append(data.AsSpan(0, length));
        var first = accumulator.GetCurrentHash();
        var second = accumulator.GetCurrentHash();
        accumulator.Append(data.AsSpan(length));
        var whole = accumulator.GetCurrentHash();

        diagnostics.Act("first read", first);
        diagnostics.Act("second read", second);
        diagnostics.Assert("hash of both halves", XxHash64.Hash(data), whole);
        Assert.AreEqual(XxHash64.Hash(data.AsSpan(0, length)), first);
        Assert.AreEqual(first, second);
        Assert.AreEqual(XxHash64.Hash(data), whole);
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(32)]
    [DataRow(70)]
    public void Reset_MidStripe_GivesWhatAFreshAccumulatorUnderTheSameSeedGives(int appendedBeforeReset)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const ulong seed = 0x9E3779B97F4A7C15;
        diagnostics.Arrange("bytes appended before reset", appendedBeforeReset);
        var accumulator = new XxHash64Accumulator(seed);
        accumulator.Append(new byte[appendedBeforeReset]);
        byte[] after = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        accumulator.Reset();
        var empty = accumulator.GetCurrentHash();
        accumulator.Append(after);
        var hash = accumulator.GetCurrentHash();

        diagnostics.Act("hash after reset", empty);
        diagnostics.Assert("hash after reset and append", XxHash64.Hash(after, seed), hash);
        Assert.AreEqual(XxHash64.Hash([], seed), empty);
        Assert.AreEqual(XxHash64.Hash(after, seed), hash);
    }

    [TestMethod]
    public void Append_ManyEmptySpans_LeavesTheHashOfNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var accumulator = new XxHash64Accumulator();

        for (var index = 0; index < 1000; index++)
        {
            accumulator.Append([]);
        }

        diagnostics.Assert("hash", XxHash64.Hash([]), accumulator.GetCurrentHash());
        Assert.AreEqual(0xEF46DB3751D8E999UL, accumulator.GetCurrentHash());
    }

    [TestMethod]
    public void Hash_SameInputUnderDifferentSeeds_GivesDifferentHashes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] data = [.. Enumerable.Range(0, 64).Select(index => (byte)index)];
        ulong[] seeds = [0, 1, long.MaxValue, ulong.MaxValue];

        var hashes = seeds.Select(seed => XxHash64.Hash(data, seed)).ToArray();

        diagnostics.Act("hashes", string.Join(", ", hashes.Select(hash => hash.ToString("X16"))));
        Assert.HasCount(seeds.Length, hashes.Distinct());
    }

    [TestMethod]
    public async Task Hash_SixteenTasksAtOnce_EachGivesTheSameHash()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var data = Enumerable.Range(0, 100_000).Select(index => (byte)(index * 31)).ToArray();
        var expected = XxHash64.Hash(data, 42);

        var hashes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => XxHash64.Hash(data, 42))));

        diagnostics.Act("distinct hashes", hashes.Distinct().Count());
        Assert.IsTrue(hashes.All(hash => hash == expected));
    }
}
