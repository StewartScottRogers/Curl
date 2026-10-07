using Curl.Testing;

namespace Curl.Zstandard;

/// <summary>
/// Checks <see cref="XxHash64Accumulator" /> gives xxhsum's published XXH64 values however
/// its input is split, and that <see cref="XxHash64Accumulator.Reset" /> starts it over.
/// <see cref="XxHash64Tests" /> feeds it every published vector a byte at a time.
/// </summary>
[TestClass]
public sealed class XxHash64AccumulatorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Append_InputSplitUnevenly_GivesTheOneShotHash()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var data = XxHash64Tests.SanityBuffer(222);
        var accumulator = new XxHash64Accumulator(XxHash64Tests.SanitySeed);
        diagnostics.Arrange("seed", XxHash64Tests.SanitySeed);
        diagnostics.Arrange("pieces", "31, 70, 0 and 121 bytes of xxhsum's 222-byte sanity buffer");
        diagnostics.Bytes("input", data);

        accumulator.Append(data.AsSpan(0, 31));
        accumulator.Append(data.AsSpan(31, 70));
        accumulator.Append([]);
        accumulator.Append(data.AsSpan(101));

        var hash = accumulator.GetCurrentHash();
        diagnostics.Act("hash", Hex(hash));
        diagnostics.Assert("hash", Hex(0x20CB8AB7AE10C14AUL), Hex(hash));
        Assert.AreEqual(0x20CB8AB7AE10C14AUL, hash);
    }

    [TestMethod]
    public void Reset_AfterInput_ForgetsItAndKeepsTheSeed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var accumulator = new XxHash64Accumulator(XxHash64Tests.SanitySeed);
        accumulator.Append(XxHash64Tests.SanityBuffer(222));
        diagnostics.Arrange("seed", XxHash64Tests.SanitySeed);
        diagnostics.Arrange("input before reset", "xxhsum's 222-byte sanity buffer");

        accumulator.Reset();

        var hash = accumulator.GetCurrentHash();
        diagnostics.Act("hash after reset", Hex(hash));
        diagnostics.Assert("hash after reset (empty input, same seed)", Hex(0xAC75FDA2929B17EFUL), Hex(hash));
        Assert.AreEqual(0xAC75FDA2929B17EFUL, hash);
    }

    [TestMethod]
    public void GetCurrentHash_NoSeedNoInput_IsTheEmptyInputHash()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("accumulator", "no seed, no input");

        var hash = new XxHash64Accumulator().GetCurrentHash();

        diagnostics.Act("hash", Hex(hash));
        diagnostics.Assert("hash", Hex(0xEF46DB3751D8E999UL), Hex(hash));
        Assert.AreEqual(0xEF46DB3751D8E999UL, hash);
    }

    internal static string Hex(ulong value) => "0x" + value.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
}
