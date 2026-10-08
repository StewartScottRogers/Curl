using Curl.Testing;

namespace Curl.Protocol.Ws;

[TestClass]
public sealed class SystemWebSocketRandomSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Fill_TwoBuffers_FillsThemDifferently()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = new SystemWebSocketRandomSource();
        byte[] first = new byte[32];
        byte[] second = new byte[32];
        diagnostics.Arrange("buffers", "two zeroed 32-byte buffers");

        source.Fill(first);
        source.Fill(second);

        bool equal = first.AsSpan().SequenceEqual(second);
        diagnostics.Bytes("first buffer", first);
        diagnostics.Bytes("second buffer", second);
        diagnostics.Act("buffers equal", equal);
        diagnostics.Assert("buffers equal", false, equal);
        CollectionAssert.AreNotEqual(first, second);
    }
}
