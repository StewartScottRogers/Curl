using System.Collections.Concurrent;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Attacks <see cref="ConnectionNumberSequence" /> under concurrent calls, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1505).
/// </summary>
[TestClass]
public sealed class ConnectionNumberSequenceAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void NumberNextConnection_OnManyTasksAtOnce_HandsOutEachNumberOnce()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        const int Calls = 10_000;
        ConnectionNumberSequence sequence = new();
        ConcurrentBag<long> numbers = [];
        diagnostics.Arrange("calls", Calls);

        Parallel.For(0, Calls, _ => numbers.Add(sequence.NumberNextConnection()));

        long[] sorted = numbers.Order().ToArray();
        bool contiguous = sorted.Select((number, index) => number == index).All(match => match);
        diagnostics.Act("distinct numbers", sorted.Distinct().Count());
        diagnostics.Assert("numbers are 0 to calls - 1", true, contiguous);
        Assert.AreEqual(Calls, sorted.Length);
        Assert.IsTrue(contiguous);
        Assert.AreEqual(Calls, sequence.NumberNextConnection());
    }
}
