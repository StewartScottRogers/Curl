using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// <see cref="ConnectionNumberSequence" /> numbers from <c>0</c>, one more each time (BL-977).
/// </summary>
[TestClass]
public sealed class ConnectionNumberSequenceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void NumberNextConnection_CountsFromZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var sequence = new ConnectionNumberSequence();
        diagnostics.Arrange("calls", 3);

        long first = sequence.NumberNextConnection();
        long second = sequence.NumberNextConnection();
        long third = sequence.NumberNextConnection();

        diagnostics.Act("numbers", $"{first}, {second}, {third}");
        diagnostics.Assert("first number", 0L, first);
        Assert.AreEqual(0L, first);
        Assert.AreEqual(1L, second);
        Assert.AreEqual(2L, third);
    }

    [TestMethod]
    public void NumberNextConnection_TwoSequences_CountApart()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var first = new ConnectionNumberSequence();
        var second = new ConnectionNumberSequence();
        diagnostics.Arrange("sequences", "two, the first advanced once");
        first.NumberNextConnection();

        long secondNumber = second.NumberNextConnection();

        diagnostics.Act("second sequence number", secondNumber);
        diagnostics.Assert("second sequence number", 0L, secondNumber);
        Assert.AreEqual(0L, secondNumber);
    }
}
