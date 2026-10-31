namespace Curl.Extended.Protocol.GoogleCloudStorage.Signing;

[TestClass]
public sealed class GcsBearerAuthorizationTests
{
    [TestMethod]
    public void Header_AccessToken_IsTheBearerAuthorizationHeader() =>
        Assert.AreEqual(new KeyValuePair<string, string>("Authorization", "Bearer ya29.a0Example"), GcsBearerAuthorization.Header(" ya29.a0Example "));

    [TestMethod]
    public void Header_EmptyOrBlankToken_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GcsBearerAuthorization.Header(null!));
        Assert.ThrowsExactly<ArgumentException>(() => GcsBearerAuthorization.Header(" "));
    }

    [TestMethod]
    public void Header_TokenWithLineBreak_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GcsBearerAuthorization.Header("a\r\nX-Injected: 1"));
        Assert.ThrowsExactly<ArgumentException>(() => GcsBearerAuthorization.Header("a\nb"));
    }
}
