using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbIdentity" /> to curl 8.21.0's <c>smb_connect</c> user and domain split.
/// </summary>
[TestClass]
public sealed class SmbIdentityTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("User", "User", "host")]
    [DataRow(@"DOM\Us", "Us", "DOM")]
    [DataRow("DOM/Us", "Us", "DOM")]
    [DataRow(@"a/b\c", @"b\c", "a")]
    public void Split_SplitsAtSlashFirstThenBackslash(string userName, string user, string domain)
    {
        Diagnostics.Arrange("user name", $"\"{userName}\", host \"host\"");

        SmbIdentity identity = SmbIdentity.Split(userName, "host");

        Diagnostics.Act("identity", identity);
        Diagnostics.Assert("identity", new SmbIdentity(user, domain), identity);
        Assert.AreEqual(new SmbIdentity(user, domain), SmbIdentity.Split(userName, "host"));
    }

}
