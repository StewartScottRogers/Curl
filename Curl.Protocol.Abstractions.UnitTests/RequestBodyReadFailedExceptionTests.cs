using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="RequestBodyReadFailedException" />: it carries its message and is an
/// <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed class RequestBodyReadFailedExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsTheMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", "read error getting mime data");

        RequestBodyReadFailedException exception = new("read error getting mime data");

        diagnostics.Act("message", exception.Message);
        diagnostics.Diff("message", "read error getting mime data", exception.Message);
        Assert.AreEqual("read error getting mime data", exception.Message);
    }

    [TestMethod]
    public void Type_IsAnIOException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("type", typeof(RequestBodyReadFailedException).Name);

        bool assignable = typeof(IOException).IsAssignableFrom(typeof(RequestBodyReadFailedException));

        diagnostics.Act("assignable to IOException", assignable);
        diagnostics.Assert("assignable to IOException", true, assignable);
        Assert.IsTrue(typeof(IOException).IsAssignableFrom(typeof(RequestBodyReadFailedException)));
    }
}
