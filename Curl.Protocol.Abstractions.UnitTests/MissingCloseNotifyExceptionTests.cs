namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="MissingCloseNotifyException" />: it carries the message it was built with
/// and is an <see cref="IOException" />, so a caller that catches only that still sees it.
/// </summary>
[TestClass]
public sealed class MissingCloseNotifyExceptionTests
{
    [TestMethod]
    public void Constructor_CarriesTheMessageAndIsAnIOException()
    {
        var exception = new MissingCloseNotifyException("schannel: server closed abruptly (missing close_notify)");

        Assert.AreEqual("schannel: server closed abruptly (missing close_notify)", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }
}
