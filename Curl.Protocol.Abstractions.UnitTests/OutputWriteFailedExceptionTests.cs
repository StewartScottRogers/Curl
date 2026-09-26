namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="OutputWriteFailedException" />: it carries its accepted byte count and
/// message, refuses a negative count, and is an <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed class OutputWriteFailedExceptionTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(96)]
    [DataRow(int.MaxValue)]
    public void Constructor_WithANonNegativeCount_RoundTripsTheCountAndMessage(int bytesAccepted)
    {
        var exception = new OutputWriteFailedException(bytesAccepted, "Standard output is closed.");

        Assert.AreEqual(bytesAccepted, exception.BytesAccepted);
        Assert.AreEqual("Standard output is closed.", exception.Message);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void Constructor_WithANegativeCount_ThrowsArgumentOutOfRangeException(int bytesAccepted)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new OutputWriteFailedException(bytesAccepted, "Standard output is closed."));

        Assert.AreEqual("bytesAccepted", exception.ParamName);
    }

    [TestMethod]
    public void Type_IsAnIOException()
    {
        Assert.IsTrue(typeof(IOException).IsAssignableFrom(typeof(OutputWriteFailedException)));
    }
}
