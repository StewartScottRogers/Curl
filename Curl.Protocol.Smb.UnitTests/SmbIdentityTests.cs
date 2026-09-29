namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbIdentity" /> to curl 8.21.0's <c>smb_connect</c> user and domain split.
/// </summary>
[TestClass]
public sealed class SmbIdentityTests
{
    [TestMethod]
    [DataRow("User", "User", "host")]
    [DataRow(@"DOM\Us", "Us", "DOM")]
    [DataRow("DOM/Us", "Us", "DOM")]
    [DataRow(@"a/b\c", @"b\c", "a")]
    public void Split_SplitsAtSlashFirstThenBackslash(string userName, string user, string domain)
    {
        Assert.AreEqual(new SmbIdentity(user, domain), SmbIdentity.Split(userName, "host"));
    }

}
