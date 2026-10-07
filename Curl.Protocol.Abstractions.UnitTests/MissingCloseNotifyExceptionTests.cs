using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="MissingCloseNotifyException" />: it carries the message it was built with
/// and is an <see cref="IOException" />, so a caller that catches only that still sees it.
/// </summary>
[TestClass]
public sealed class MissingCloseNotifyExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_CarriesTheMessageAndIsAnIOException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", "schannel: server closed abruptly (missing close_notify)");

        var exception = new MissingCloseNotifyException("schannel: server closed abruptly (missing close_notify)");

        diagnostics.Act("message", exception.Message);
        diagnostics.Act("is IOException", exception is IOException);
        diagnostics.Diff("message", "schannel: server closed abruptly (missing close_notify)", exception.Message);
        Assert.AreEqual("schannel: server closed abruptly (missing close_notify)", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }
}
