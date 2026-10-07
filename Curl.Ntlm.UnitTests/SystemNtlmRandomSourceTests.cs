using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>Checks <see cref="SystemNtlmRandomSource" /> fills what it is given.</summary>
[TestClass]
public sealed class SystemNtlmRandomSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Fill_TwoBuffers_GivesDifferentBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] first = new byte[32];
        byte[] second = new byte[32];
        SystemNtlmRandomSource source = new();
        diagnostics.Arrange("buffer lengths", "32 and 32 zero bytes");

        source.Fill(first);
        source.Fill(second);
        diagnostics.Bytes("first", first);
        diagnostics.Bytes("second", second);
        diagnostics.Act("buffers equal", first.AsSpan().SequenceEqual(second));

        diagnostics.Diff("second against first (expected to differ)", first, second);
        CollectionAssert.AreNotEqual(first, second);
    }
}
