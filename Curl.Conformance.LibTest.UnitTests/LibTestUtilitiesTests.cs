namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestUtilitiesTests
{
    [TestMethod]
    [DataRow(1, "http://127.0.0.1/0001")]
    [DataRow(12345, "http://127.0.0.1/12345")]
    [DataRow(-1, "http://127.0.0.1/-0001")]
    public void SubUrl_AppendsTheNumberInAtLeastFourDigits(int number, string expected)
    {
        string url = LibTestUtilities.SubUrl("http://127.0.0.1/", number);

        Assert.AreEqual(expected, url);
    }

    [TestMethod]
    public void SubUrl_RefusesANullBase()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibTestUtilities.SubUrl(null!, 1));
    }

    [TestMethod]
    public void ThrowawayWrite_TakesEveryByte()
    {
        long taken = LibTestUtilities.ThrowawayWrite("hello"u8);

        Assert.AreEqual(5L, taken);
    }
}
