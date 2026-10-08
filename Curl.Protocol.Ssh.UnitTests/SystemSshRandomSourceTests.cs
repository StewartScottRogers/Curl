using Curl.Testing;

namespace Curl.Protocol.Ssh;

[TestClass]
public sealed class SystemSshRandomSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Fill_WritesBytesThatDifferBetweenCalls()
    {
        SystemSshRandomSource source = new();
        byte[] first = new byte[32];
        byte[] second = new byte[32];
        Diagnostics.Arrange("buffers", "two of 32 bytes");

        source.Fill(first);
        source.Fill(second);

        Diagnostics.Bytes("first", first);
        Diagnostics.Bytes("second", second);
        Diagnostics.Act("first equals second", first.SequenceEqual(second));
        Diagnostics.Assert("first differs from second", true, !first.SequenceEqual(second));
        CollectionAssert.AreNotEqual(first, second);
    }
}
