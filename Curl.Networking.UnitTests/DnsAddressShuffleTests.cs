namespace Curl.Networking;

[TestClass]
public sealed class DnsAddressShuffleTests
{
    [TestMethod]
    public void Shuffle_RandomIndexAlwaysZero_SwapsEachAddressWithTheFirstFromTheLastDown()
    {
        List<int> bounds = [];

        int[] shuffled = DnsAddressShuffle.Shuffle([1, 2, 3, 4], bound =>
        {
            bounds.Add(bound);
            return 0;
        });

        CollectionAssert.AreEqual(new[] { 2, 3, 4, 1 }, shuffled);
        CollectionAssert.AreEqual(new[] { 4, 3, 2 }, bounds);
    }

    [TestMethod]
    public void Shuffle_RandomIndexIsTheTop_KeepsTheOrderAndLeavesTheListUnchanged()
    {
        int[] addresses = [1, 2, 3];

        int[] shuffled = DnsAddressShuffle.Shuffle(addresses, bound => bound - 1);

        CollectionAssert.AreEqual(addresses, shuffled);
        Assert.AreNotSame(addresses, shuffled);
    }

    [TestMethod]
    public void Shuffle_OneAddress_AsksForNoRandomIndex()
    {
        CollectionAssert.AreEqual(new[] { 7 }, DnsAddressShuffle.Shuffle([7], _ => throw new AssertFailedException("no random index wanted")));
    }

    [TestMethod]
    public void CryptographicRandomBelow_GivesAnIndexBelowTheBound()
    {
        int index = DnsAddressShuffle.CryptographicRandomBelow(3);

        Assert.IsTrue(index is >= 0 and < 3);
    }

    [TestMethod]
    public void Shuffle_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsAddressShuffle.Shuffle<int>(null!, _ => 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsAddressShuffle.Shuffle([1], null!));
    }
}
