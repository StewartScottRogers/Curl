using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="OutputWriteFailedException" />: it carries its accepted byte count and
/// message, refuses a negative count, and is an <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed class OutputWriteFailedExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0)]
    [DataRow(96)]
    [DataRow(int.MaxValue)]
    public void Constructor_WithANonNegativeCount_RoundTripsTheCountAndMessage(int bytesAccepted)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes accepted", bytesAccepted);
        diagnostics.Arrange("message", "Standard output is closed.");

        var exception = new OutputWriteFailedException(bytesAccepted, "Standard output is closed.");

        diagnostics.Act("bytes accepted", exception.BytesAccepted);
        diagnostics.Act("message", exception.Message);
        diagnostics.Assert("bytes accepted", bytesAccepted, exception.BytesAccepted);
        diagnostics.Diff("message", "Standard output is closed.", exception.Message);
        Assert.AreEqual(bytesAccepted, exception.BytesAccepted);
        Assert.AreEqual("Standard output is closed.", exception.Message);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void Constructor_WithANegativeCount_ThrowsArgumentOutOfRangeException(int bytesAccepted)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes accepted", bytesAccepted);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new OutputWriteFailedException(bytesAccepted, "Standard output is closed."));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Diff("param name", "bytesAccepted", exception.ParamName ?? string.Empty);
        Assert.AreEqual("bytesAccepted", exception.ParamName);
    }

    [TestMethod]
    public void Type_IsAnIOException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("type", typeof(OutputWriteFailedException).Name);

        bool assignable = typeof(IOException).IsAssignableFrom(typeof(OutputWriteFailedException));

        diagnostics.Act("assignable to IOException", assignable);
        diagnostics.Assert("assignable to IOException", true, assignable);
        Assert.IsTrue(typeof(IOException).IsAssignableFrom(typeof(OutputWriteFailedException)));
    }
}
