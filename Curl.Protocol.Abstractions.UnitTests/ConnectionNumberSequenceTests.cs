namespace Curl.Protocol.Abstractions;

/// <summary>
/// <see cref="ConnectionNumberSequence" /> numbers from <c>0</c>, one more each time (BL-977).
/// </summary>
[TestClass]
public sealed class ConnectionNumberSequenceTests
{
    [TestMethod]
    public void NumberNextConnection_CountsFromZero()
    {
        var sequence = new ConnectionNumberSequence();

        Assert.AreEqual(0L, sequence.NumberNextConnection());
        Assert.AreEqual(1L, sequence.NumberNextConnection());
        Assert.AreEqual(2L, sequence.NumberNextConnection());
    }

    [TestMethod]
    public void NumberNextConnection_TwoSequences_CountApart()
    {
        var first = new ConnectionNumberSequence();
        var second = new ConnectionNumberSequence();
        first.NumberNextConnection();

        Assert.AreEqual(0L, second.NumberNextConnection());
    }
}
