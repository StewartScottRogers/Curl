using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="CurlErrorBuffer" />: a message of up to 255 characters is kept whole, and a
/// longer one is cut to its first 255, as curl 8.21.0's 256-byte error buffer cuts it.
/// </summary>
[TestClass]
public sealed class CurlErrorBufferTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(0)]
    [DataRow(254)]
    [DataRow(255)]
    public void Truncate_WithMessageOfAtMost255Characters_ReturnsItWhole(int length)
    {
        Diagnostics.Arrange("message length", length);
        var message = new string('m', length);

        var truncated = CurlErrorBuffer.Truncate(message);

        Diagnostics.Act("truncated length", truncated.Length);
        Diagnostics.Act("same instance returned", ReferenceEquals(message, truncated));
        Diagnostics.Assert("same instance returned", true, ReferenceEquals(message, truncated));
        Assert.AreSame(message, CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    [DataRow(256)]
    [DataRow(65559)]
    public void Truncate_WithMessageLongerThan255Characters_ReturnsItsFirst255(int length)
    {
        Diagnostics.Arrange("message length", length);
        var message = "Could not resolve host: " + new string('a', length - 24);

        var truncated = CurlErrorBuffer.Truncate(message);

        Diagnostics.Act("truncated length", truncated.Length);
        Diagnostics.Diff("truncated message", message[..255], truncated);
        Assert.AreEqual(message[..255], CurlErrorBuffer.Truncate(message));
    }
}
