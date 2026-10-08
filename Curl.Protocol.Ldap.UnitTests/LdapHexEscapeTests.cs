using Curl.Testing;

namespace Curl.Protocol.Ldap;

[TestClass]
public sealed class LdapHexEscapeTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("%2a", 0, '%', '*')]
    [DataRow("x\\C3", 1, '\\', 'Ã')]
    public void TryRead_IntroducerAndTwoHexDigits_ReadsTheByte(string text, int index, char introducer, char expected)
    {
        Diagnostics.Arrange("text", text);
        Diagnostics.Arrange("index", index);
        Diagnostics.Arrange("introducer", introducer);

        bool read = LdapHexEscape.TryRead(text, index, introducer, out char octet);

        Diagnostics.Act("read", read);
        Diagnostics.Act("octet", (int)octet);
        Diagnostics.Assert("octet", (int)expected, (int)octet);
        Assert.IsTrue(read);
        Assert.AreEqual(expected, octet);
    }

    [TestMethod]
    [DataRow("x2a", '%')]
    [DataRow("%2", '%')]
    [DataRow("%g0", '%')]
    [DataRow("%0g", '%')]
    [DataRow("\\2a", '%')]
    public void TryRead_NoEscapeThere_ReturnsFalse(string text, char introducer)
    {
        Diagnostics.Arrange("text", text);
        Diagnostics.Arrange("introducer", introducer);

        bool read = LdapHexEscape.TryRead(text, 0, introducer, out _);

        Diagnostics.Act("read", read);
        Diagnostics.Assert("read", false, read);
        Assert.IsFalse(read);
    }
}
