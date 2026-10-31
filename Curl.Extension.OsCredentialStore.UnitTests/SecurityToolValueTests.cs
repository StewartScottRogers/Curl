namespace Curl.Extension.OsCredentialStore.UnitTests;

[TestClass]
public sealed class SecurityToolValueTests
{
    [TestMethod]
    public void ValueOf_QuotedText_IsTheTextInside() =>
        Assert.AreEqual("alice", SecurityToolValue.ValueOf("\"alice\""));

    [TestMethod]
    public void ValueOf_Hexadecimal_IsItsBytesAsUtf8() =>
        Assert.AreEqual("hi", SecurityToolValue.ValueOf("0x6869  \"hi\""));

    [TestMethod]
    public void ValueOf_NullOrUnquoted_IsEmpty()
    {
        Assert.AreEqual(string.Empty, SecurityToolValue.ValueOf("<NULL>"));
        Assert.AreEqual(string.Empty, SecurityToolValue.ValueOf("\""));
        Assert.AreEqual(string.Empty, SecurityToolValue.ValueOf("\"open"));
    }

    [TestMethod]
    public void After_NoLineHasThePrefix_IsNone() =>
        Assert.IsNull(SecurityToolValue.After("class: \"genp\"\n", "\"acct\"<blob>="));
}
