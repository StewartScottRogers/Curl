using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Telnet;

/// <summary>
/// Pins CGR-007 (BL-3536): a <c>-T</c> upload goes out from the telnet loop after the replies
/// to the server's negotiation, never before them nor between the writes of one reply, so no
/// upload byte lands inside <c>IAC SB NAWS ... IAC SE</c>. curl 8.22.0 sent the replies to a
/// server's opening <c>DO NAWS</c> before any upload byte in 40 of 40 Windows runs.
/// </summary>
[TestClass]
public sealed class Cgr007Tests
{
    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_UploadAndServerDoNaws_SendsTheNawsRepliesWholeBeforeTheUpload()
    {
        byte[] upload = "hello telnet\r\n"u8.ToArray();
        var connection = new ScriptedConnection(new ScriptedRead([0xFF, 0xFD, 0x1F]));
        Diagnostics.Arrange("server", "sends IAC DO NAWS, then closes");
        Diagnostics.ArrangeUpload(upload);

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = new MemoryStream(), Upload = new MemoryStream(upload) });
        Diagnostics.ActResult(result);

        string expected = "FFFB1F" + "FFFA1F00000000FFF0" + "FFFB00FFFD00FFFB03FFFD03" + Convert.ToHexString(upload);
        string sent = Convert.ToHexString(connection.Sent);
        Diagnostics.Diff("sent", expected, sent);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, sent);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAndUploadWriteFails_EndsWithExit0()
    {
        var connection = new FaultingConnection { ReadFailsAfterReads = true, WritesFail = true };
        Diagnostics.Arrange("server", "resets at once; every write fails");
        Diagnostics.ArrangeUpload("a\n"u8.ToArray());

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = new MemoryStream(), Upload = new MemoryStream("a\n"u8.ToArray()) });
        Diagnostics.ActResult(result);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }
}
