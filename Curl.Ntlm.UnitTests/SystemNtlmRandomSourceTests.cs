namespace Curl.Ntlm;

/// <summary>Checks <see cref="SystemNtlmRandomSource" /> fills what it is given.</summary>
[TestClass]
public sealed class SystemNtlmRandomSourceTests
{
    [TestMethod]
    public void Fill_TwoBuffers_GivesDifferentBytes()
    {
        byte[] first = new byte[32];
        byte[] second = new byte[32];
        SystemNtlmRandomSource source = new();

        source.Fill(first);
        source.Fill(second);

        CollectionAssert.AreNotEqual(first, second);
    }
}
