using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>Pins <see cref="LdapBindReply.IsSuccess" />: only a BindResponse with resultCode <c>success</c> is a bound session.</summary>
[TestClass]
public sealed class LdapBindReplyTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((int)LdapBindReplyStatus.Answered, 0, true)]
    [DataRow((int)LdapBindReplyStatus.Answered, 49, false)]
    [DataRow((int)LdapBindReplyStatus.Closed, 0, false)]
    [DataRow((int)LdapBindReplyStatus.Malformed, 0, false)]
    public void IsSuccess_OnlyForAnAnsweredSuccess(int status, int resultCode, bool expected)
    {
        Diagnostics.Arrange("status", (LdapBindReplyStatus)status);
        Diagnostics.Arrange("result code", resultCode);

        bool actual = new LdapBindReply((LdapBindReplyStatus)status, resultCode).IsSuccess;

        Diagnostics.Act("IsSuccess", actual);
        Diagnostics.Assert("IsSuccess", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
