using System.Text;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlErrorBuffer" />: a message of up to 255 UTF-8 bytes is kept whole, a
/// longer one is cut to 255 bytes, and a character the limit falls inside is dropped whole.
/// </summary>
[TestClass]
public sealed class CurlErrorBufferTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Truncate_Null_ReturnsNull()
    {
        Diagnostics.Arrange("message", "null");

        string? truncated = CurlErrorBuffer.Truncate(null);
        Diagnostics.Act("truncated", truncated ?? "null");

        Diagnostics.Assert("truncated", "null", truncated ?? "null");
        Assert.IsNull(CurlErrorBuffer.Truncate(null));
    }

    [TestMethod]
    public void Truncate_MessageOf255Bytes_ReturnsItWhole()
    {
        string message = new('a', 255);
        Diagnostics.Arrange("message UTF-8 bytes", Encoding.UTF8.GetByteCount(message));

        string? truncated = CurlErrorBuffer.Truncate(message);
        Diagnostics.Act("same instance returned", ReferenceEquals(message, truncated));

        Diagnostics.Assert("same instance returned", true, ReferenceEquals(message, truncated));
        Assert.AreSame(message, CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    public void Truncate_AsciiMessageOver255Bytes_ReturnsFirst255()
    {
        string message = new('a', 300);
        Diagnostics.Arrange("message UTF-8 bytes", Encoding.UTF8.GetByteCount(message));

        string? truncated = CurlErrorBuffer.Truncate(message);
        Diagnostics.Act("truncated length", truncated?.Length ?? -1);

        Diagnostics.Diff("truncated", message[..255], truncated ?? string.Empty);
        Assert.AreEqual(message[..255], CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    public void Truncate_TwoByteCharacterAcrossTheLimit_DropsItWhole()
    {
        // 254 ASCII bytes, then "é" (C3 A9) takes bytes 255 and 256: only its first fits.
        string message = new string('a', 254) + "éé";
        Diagnostics.Arrange("message UTF-8 bytes", Encoding.UTF8.GetByteCount(message));

        string? truncated = CurlErrorBuffer.Truncate(message);
        Diagnostics.Act("truncated length", truncated?.Length ?? -1);

        Diagnostics.Diff("truncated", new string('a', 254), truncated ?? string.Empty);
        Assert.AreEqual(new string('a', 254), CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    public void Truncate_ThreeByteCharacterEndingAtTheLimit_KeepsIt()
    {
        // 252 ASCII bytes, then "€" (E2 82 AC) is bytes 253 to 255.
        string message = new string('a', 252) + "€€";
        Diagnostics.Arrange("message UTF-8 bytes", Encoding.UTF8.GetByteCount(message));

        string? truncated = CurlErrorBuffer.Truncate(message);
        Diagnostics.Act("truncated UTF-8 bytes", Encoding.UTF8.GetByteCount(truncated ?? string.Empty));

        Diagnostics.Diff("truncated", new string('a', 252) + "€", truncated ?? string.Empty);
        Assert.AreEqual(new string('a', 252) + "€", CurlErrorBuffer.Truncate(message));
    }
}
