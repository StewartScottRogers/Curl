using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>Pins <see cref="SortingNetwork" /> against <see cref="Array.Sort(Array)" /> for every length the network shape changes at.</summary>
[TestClass]
public sealed class SortingNetworkTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(17)]
    [DataRow(761)]
    public void Sort_RandomWords_MatchesArraySort(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Random random = new(length);
        uint[] values = new uint[length];
        for (int index = 0; index < length; index++)
        {
            values[index] = (uint)random.NextInt64(0, 1L << 32);
        }

        values[0..Math.Min(2, length)].AsSpan().Fill(0x80000000u);
        uint[] expected = (uint[])values.Clone();
        Array.Sort(expected);
        diagnostics.Arrange("source", "random words seeded with the length, checked against Array.Sort");
        diagnostics.Arrange("length", length);
        diagnostics.Arrange("first words", string.Join(' ', values.Take(8).Select(value => value.ToString("X8"))));

        SortingNetwork.Sort(values);
        diagnostics.Act("first sorted words", string.Join(' ', values.Take(8).Select(value => value.ToString("X8"))));

        diagnostics.Diff(
            "sorted words",
            string.Join(' ', expected.Select(value => value.ToString("X8"))),
            string.Join(' ', values.Select(value => value.ToString("X8"))));
        CollectionAssert.AreEqual(expected, values);
    }
}
