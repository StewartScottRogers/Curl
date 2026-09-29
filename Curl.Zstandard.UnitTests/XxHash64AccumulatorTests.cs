namespace Curl.Zstandard;

/// <summary>
/// Checks <see cref="XxHash64Accumulator" /> gives xxhsum's published XXH64 values however
/// its input is split, and that <see cref="XxHash64Accumulator.Reset" /> starts it over.
/// <see cref="XxHash64Tests" /> feeds it every published vector a byte at a time.
/// </summary>
[TestClass]
public sealed class XxHash64AccumulatorTests
{
    [TestMethod]
    public void Append_InputSplitUnevenly_GivesTheOneShotHash()
    {
        var data = XxHash64Tests.SanityBuffer(222);
        var accumulator = new XxHash64Accumulator(XxHash64Tests.SanitySeed);

        accumulator.Append(data.AsSpan(0, 31));
        accumulator.Append(data.AsSpan(31, 70));
        accumulator.Append([]);
        accumulator.Append(data.AsSpan(101));

        Assert.AreEqual(0x20CB8AB7AE10C14AUL, accumulator.GetCurrentHash());
    }

    [TestMethod]
    public void Reset_AfterInput_ForgetsItAndKeepsTheSeed()
    {
        var accumulator = new XxHash64Accumulator(XxHash64Tests.SanitySeed);
        accumulator.Append(XxHash64Tests.SanityBuffer(222));

        accumulator.Reset();

        Assert.AreEqual(0xAC75FDA2929B17EFUL, accumulator.GetCurrentHash());
    }

    [TestMethod]
    public void GetCurrentHash_NoSeedNoInput_IsTheEmptyInputHash()
    {
        Assert.AreEqual(0xEF46DB3751D8E999UL, new XxHash64Accumulator().GetCurrentHash());
    }
}
