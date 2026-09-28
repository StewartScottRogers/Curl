namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="RequestBodyReadFailedException" />: it carries its message and is an
/// <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed class RequestBodyReadFailedExceptionTests
{
    [TestMethod]
    public void Constructor_RoundTripsTheMessage()
    {
        RequestBodyReadFailedException exception = new("read error getting mime data");

        Assert.AreEqual("read error getting mime data", exception.Message);
    }

    [TestMethod]
    public void Type_IsAnIOException()
    {
        Assert.IsTrue(typeof(IOException).IsAssignableFrom(typeof(RequestBodyReadFailedException)));
    }
}
