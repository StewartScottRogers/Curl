using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins <c>-I</c> on the WinLDAP dialect ending in exit 8 "Weird server reply" with empty output,
/// while the OpenLDAP dialect still ends in exit 0 (BL-5397).
/// </summary>
[TestClass]
public sealed class LdapProtocolHandlerWinLdapWeirdReplyTests
{
    [TestMethod]
    public async Task ExecuteAsync_NoBodyOnWinLdap_FailsWithWeirdServerReplyAndWritesNothing()
    {
        (TransferResult result, byte[] output, _) = await LdapNoBodyTests.RunAsync(LdapDialect.WinLdap, noBody: true);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyOnOpenLdap_SucceedsWithNoOutput()
    {
        (TransferResult result, byte[] output, _) = await LdapNoBodyTests.RunAsync(LdapDialect.OpenLdap, noBody: true);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
    }
}
