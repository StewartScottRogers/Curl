using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>
/// Checks <see cref="NtlmUserName.SplitDomain" /> splits as curl 8.21.0's
/// <c>Curl_auth_create_ntlm_type3_message</c> does: at the first backslash, else the first
/// slash.
/// </summary>
[TestClass]
public sealed class NtlmUserNameTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("user", "", "user")]
    [DataRow(@"DOMAIN\user", "DOMAIN", "user")]
    [DataRow("DOMAIN/user", "DOMAIN", "user")]
    [DataRow(@"a/b\c", "a/b", "c")]
    [DataRow(@"a\b/c", "a", "b/c")]
    [DataRow(@"\user", "", "user")]
    public void SplitDomain_UserName_SplitsAtTheFirstBackslashElseTheFirstSlash(string userName, string domain, string user)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user name", userName);

        (string Domain, string User) split = NtlmUserName.SplitDomain(userName);
        diagnostics.Act("split", $"domain '{split.Domain}', user '{split.User}'");

        diagnostics.Diff("domain", domain, split.Domain);
        diagnostics.Diff("user", user, split.User);
        Assert.AreEqual(domain, split.Domain);
        Assert.AreEqual(user, split.User);
    }
}
