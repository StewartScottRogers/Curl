namespace Curl.Mcp;

[TestClass]
public sealed class McpStandardErrorLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 14, 30, 5, 123, TimeSpan.FromHours(2));

    [TestMethod]
    public void WriteLine_WritesTheMessageAfterItsUtcTimeAndFlushes()
    {
        using StringWriter error = new();
        McpStandardErrorLog log = new(error, new FixedTimeProvider(Now));

        log.WriteLine("Curl.Mcp stopped.");

        Assert.AreEqual("2026-10-23T12:30:05.123Z Curl.Mcp stopped." + Environment.NewLine, error.ToString());
    }

    [TestMethod]
    public void WriteLine_FromManyThreadsAtOnce_WritesEveryLineWhole()
    {
        using StringWriter error = new();
        McpStandardErrorLog log = new(error, new FixedTimeProvider(Now));

        Parallel.For(0, 200, index => log.WriteLine($"message {index}"));

        string[] lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(200, lines);
        CollectionAssert.AreEquivalent(
            Enumerable.Range(0, 200).Select(index => $"2026-10-23T12:30:05.123Z message {index}").ToArray(),
            lines);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
