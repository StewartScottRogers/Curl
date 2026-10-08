using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContentInput" />: a read returns what is pending, then nothing, and
/// every other stream operation is unsupported.
/// </summary>
[TestClass]
public sealed class HttpContentInputTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Read_Pending_ReturnsItThenNothing()
    {
        using HttpContentInput input = new() { Pending = new byte[] { 1, 2, 3 } };
        byte[] buffer = new byte[4];
        Diagnostics.Arrange("pending", "01 02 03");

        int first = input.Read(buffer, 1, 2);
        int second = input.Read(buffer, 3, 1);
        int third = input.Read(buffer, 0, 4);

        Diagnostics.Act("reads", $"{first}, {second}, {third}");
        Diagnostics.Bytes("buffer", buffer);
        Diagnostics.Assert("reads", "2, 1, 0", $"{first}, {second}, {third}");
        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(0, third);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, buffer);
    }

    [TestMethod]
    public void Capabilities_ReadOnlyAndNotSeekable()
    {
        using HttpContentInput input = new();
        Diagnostics.Arrange("input", "nothing pending");

        input.Flush();

        Diagnostics.Act("capabilities", $"read {input.CanRead}, seek {input.CanSeek}, write {input.CanWrite}");
        Diagnostics.Assert("capabilities", "read True, seek False, write False", $"read {input.CanRead}, seek {input.CanSeek}, write {input.CanWrite}");
        Assert.IsTrue(input.CanRead);
        Assert.IsFalse(input.CanSeek);
        Assert.IsFalse(input.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => input.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => input.Write([1], 0, 1));
    }
}
