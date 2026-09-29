namespace Curl.Cryptography;

/// <summary>Pins <see cref="SortingNetwork" /> against <see cref="Array.Sort(Array)" /> for every length the network shape changes at.</summary>
[TestClass]
public sealed class SortingNetworkTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(17)]
    [DataRow(761)]
    public void Sort_RandomWords_MatchesArraySort(int length)
    {
        Random random = new(length);
        uint[] values = new uint[length];
        for (int index = 0; index < length; index++)
        {
            values[index] = (uint)random.NextInt64(0, 1L << 32);
        }

        values[0..Math.Min(2, length)].AsSpan().Fill(0x80000000u);
        uint[] expected = (uint[])values.Clone();
        Array.Sort(expected);

        SortingNetwork.Sort(values);

        CollectionAssert.AreEqual(expected, values);
    }
}
