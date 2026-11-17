using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins <see cref="ITransferContext.NoBody" /> (<c>-I</c>) on a search: the bind and the search
/// still go on the wire, but no entry is written to the output (BL-5333).
/// </summary>
[TestClass]
public sealed class LdapNoBodyTests
{
    private const string BindSuccess1 = "30 0c 02 01 01 61 07 0a 01 00 04 00 04 00";

    private const string EntryDcA = "30 18 02 01 02 64 13 04 04 64 63 3d 61 30 0b 30 09 04 02 63 6e 31 03 04 01 78";

    private const string EntryDcB = "30 18 02 01 02 64 13 04 04 64 63 3d 62 30 0b 30 09 04 02 63 6e 31 03 04 01 79";

    private const string SearchSuccess = "30 0c 02 01 02 65 07 0a 01 00 04 00 04 00";

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_NoBodyWithTwoEntries_WritesNothingButStillSearchesAndSucceeds(LdapDialect dialect)
    {
        (TransferResult result, byte[] output, int sentLength) = await RunAsync(dialect, noBody: true);
        (_, _, int sentWithBody) = await RunAsync(dialect, noBody: false);

        Assert.AreEqual(0, output.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(sentLength > 0);
        Assert.AreEqual(sentWithBody, sentLength, "The bind and SearchRequest are sent as without -I.");
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_WithoutNoBodyAndTwoEntries_WritesBothEntries(LdapDialect dialect)
    {
        (TransferResult result, byte[] output, _) = await RunAsync(dialect, noBody: false);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string text = System.Text.Encoding.ASCII.GetString(output);
        StringAssert.Contains(text, "dc=a");
        StringAssert.Contains(text, "dc=b");
    }

    private static async Task<(TransferResult Result, byte[] Output, int SentLength)> RunAsync(LdapDialect dialect, bool noBody)
    {
        var connection = new ScriptedConnection([Hex.Bytes(BindSuccess1), Hex.Bytes(EntryDcA + " " + EntryDcB + " " + SearchSuccess)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect);
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:38901/x"),
            Output = output,
            Credentials = new NetworkCredential("cn=u", "p"),
            NoBody = noBody,
        };

        TransferResult result = await handler.ExecuteAsync(context);
        return (result, output.ToArray(), connection.Sent.Length);
    }
}
