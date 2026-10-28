using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-179 (BL-3708): curl 8.22.0 cuts a message at 1023 bytes before it wraps it
/// (measured on Windows, 2026-10-28: a 3000-byte <c>--dump-header</c> name prints 15 lines, a
/// 3000-byte <c>--cacert</c> name 15 lines before the try-help lines).
/// </summary>
[TestClass]
public sealed class Cgr179Tests
{
    [TestMethod]
    public void Lines_MessageOf3000Bytes_IsCutAt1023BytesBeforeWrapping()
    {
        string message = "Failed to open " + new string('x', 3000);

        IReadOnlyList<string> lines = WrappedMessage.Lines("curl: ", message, 79);

        Assert.HasCount(15, lines);
        Assert.AreEqual(21, lines[0].Length);
        Assert.AreEqual(59, lines[^1].Length - "curl: ".Length);
        Assert.AreEqual(1023, lines.Sum(line => Encoding.UTF8.GetByteCount(line) - "curl: ".Length));
    }

    [TestMethod]
    public void Lines_MessageOfExactly1023Bytes_IsNotCut()
    {
        IReadOnlyList<string> lines = WrappedMessage.Lines("curl: ", new string('y', 1023), 79);

        Assert.AreEqual(1023, lines.Sum(line => line.Length - "curl: ".Length));
    }
}
