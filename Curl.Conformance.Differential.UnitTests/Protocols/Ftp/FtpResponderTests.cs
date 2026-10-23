using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Ftp;
using Curl.Conformance.Differential.UnitTests.Http.Core;
using Curl.Conformance.Differential.UnitTests.Protocols.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Ftp;

[TestClass]
public sealed class FtpResponderTests
{
    private const string Banner = "220 mock ftp\r\n";

    [TestMethod]
    [DataRow("USER anonymous", "331 pass please\r\n")]
    [DataRow("USER x", "331 pass please\r\n")]
    [DataRow("PASS right", "230 logged in\r\n")]
    [DataRow("PASS wrong", "530 login incorrect\r\n")]
    [DataRow("PWD", "257 \"/\" is cwd\r\n")]
    [DataRow("SYST", "215 UNIX Type: L8\r\n")]
    [DataRow("FEAT", "211-Features:\r\n SIZE\r\n MDTM\r\n REST STREAM\r\n EPSV\r\n211 End\r\n")]
    [DataRow("CWD /dir", "250 ok\r\n")]
    [DataRow("TYPE I", "200 Type I\r\n")]
    [DataRow("OPTS UTF8 ON", "200 ok\r\n")]
    [DataRow("SIZE a.txt", "213 11\r\n")]
    [DataRow("SIZE /dir/b.txt", "213 6\r\n")]
    [DataRow("SIZE nope", "550 no such file\r\n")]
    [DataRow("MDTM a.txt", "213 20151021072800\r\n")]
    [DataRow("MDTM //a.txt", "213 20151021072800\r\n")]
    [DataRow("MDTM nope", "550 no\r\n")]
    [DataRow("REST 5", "350 restarting\r\n")]
    [DataRow("DELE x", "250 ok\r\n")]
    [DataRow("RMD x", "250 ok\r\n")]
    [DataRow("MKD x", "250 ok\r\n")]
    [DataRow("RNFR x", "350 ready\r\n")]
    [DataRow("RNTO x", "250 ok\r\n")]
    [DataRow("SITE CHMOD", "200 ok\r\n")]
    [DataRow("NOOP", "200 ok\r\n")]
    [DataRow("noop", "200 ok\r\n")]
    [DataRow("BOGUS", "502 not implemented\r\n")]
    [DataRow("QUIT", "221 bye\r\n")]
    public async Task RespondAsync_Command_AnswersTheReplyAndLogsTheLine(string line, string reply)
    {
        var (output, log, _) = await RunAsync(line + "\r\n");

        Assert.AreEqual(Banner + reply, output);
        Assert.AreEqual(line, log);
    }

    [TestMethod]
    public async Task RespondAsync_PasvAndEpsv_TellThePort()
    {
        var (output, _, opener) = await RunAsync("PASV\r\nEPSV\r\n");

        Assert.AreEqual(Banner + "227 Entering Passive Mode (127,0,0,1,157,66)\r\n229 Entering Extended Passive Mode (|||40259|)\r\n", output);
        Assert.IsTrue(opener.Opened[0].Disposed);
        Assert.HasCount(2, opener.Opened);
    }

    [TestMethod]
    public async Task RespondAsync_List_SendsTheLongListing()
    {
        var (output, _, opener) = await RunAsync("PASV\r\nLIST\r\n");

        StringAssert.EndsWith(output, "150 here comes\r\n226 done\r\n", StringComparison.Ordinal);
        Assert.AreEqual(
            "-rw-r--r--   1 u g 11 Oct 21 07:28 a.txt\r\n-rw-r--r--   1 u g 2 Oct 21 07:28 dir\r\n-rw-r--r--   1 u g 102400 Oct 21 07:28 big.bin\r\n",
            Encoding.ASCII.GetString(opener.Opened[0].Channel!.Written));
    }

    [TestMethod]
    public async Task RespondAsync_Nlst_SendsTheNames()
    {
        var (_, _, opener) = await RunAsync("EPSV\r\nNLST\r\n");

        Assert.AreEqual("a.txt\r\ndir\r\nbig.bin\r\n", Encoding.ASCII.GetString(opener.Opened[0].Channel!.Written));
    }

    [TestMethod]
    public async Task RespondAsync_Retr_SendsTheFileFromTheRestartOffsetOnce()
    {
        var (output, _, opener) = await RunAsync("EPSV\r\nREST 6\r\nRETR a.txt\r\nEPSV\r\nRETR /a.txt\r\n");

        StringAssert.Contains(output, "150 opening\r\n226 done\r\n", StringComparison.Ordinal);
        Assert.AreEqual("file\n", Encoding.ASCII.GetString(opener.Opened[0].Channel!.Written));
        Assert.AreEqual("alpha file\n", Encoding.ASCII.GetString(opener.Opened[1].Channel!.Written));
    }

    [TestMethod]
    public async Task RespondAsync_RetrRelativeToCwd_JoinsTheDirectory()
    {
        var (_, _, opener) = await RunAsync("CWD /dir/\r\nEPSV\r\nRETR b.txt\r\n");

        Assert.AreEqual("bravo\n", Encoding.ASCII.GetString(opener.Opened[0].Channel!.Written));
    }

    [TestMethod]
    public async Task RespondAsync_RetrOffsets_FollowPythonSlicing()
    {
        var (_, _, opener) = await RunAsync("EPSV\r\nREST -6\r\nRETR a.txt\r\nEPSV\r\nREST -99\r\nRETR a.txt\r\nEPSV\r\nREST 99\r\nRETR a.txt\r\n");

        Assert.AreEqual(" file\n", Encoding.ASCII.GetString(opener.Opened[0].Channel!.Written));
        Assert.AreEqual("alpha file\n", Encoding.ASCII.GetString(opener.Opened[1].Channel!.Written));
        Assert.IsEmpty(opener.Opened[2].Channel!.Written);
    }

    [TestMethod]
    public async Task RespondAsync_RetrMissingFile_Answers550AndCarriesOn()
    {
        var (output, _, _) = await RunAsync("RETR nope\r\nNOOP\r\n");

        Assert.AreEqual(Banner + "550 no such file\r\n200 ok\r\n", output);
    }

    [TestMethod]
    public async Task RespondAsync_StorAndAppe_LogTheUploadedBytes()
    {
        var upload = Encoding.ASCII.GetBytes(new string('x', 50));
        var (output, log, _) = await RunAsync("EPSV\r\nSTOR f\r\n", upload);

        StringAssert.Contains(output, "150 ok send\r\n226 stored\r\n", StringComparison.Ordinal);
        Assert.AreEqual($"EPSV\nSTOR f\n[uploaded 50 bytes: b'{new string('x', 40)}']", log);

        var (_, appendLog, _) = await RunAsync("PASV\r\nAPPE f\r\n", "hi\r\n"u8.ToArray());
        Assert.AreEqual("PASV\nAPPE f\n[uploaded 4 bytes: b'hi\\r\\n']", appendLog);
    }

    [TestMethod]
    [DataRow("REST abc")]
    [DataRow("LIST")]
    [DataRow("RETR a.txt")]
    [DataRow("STOR f")]
    public async Task RespondAsync_CommandThePythonHandlerCrashesOn_EndsTheConnection(string line)
    {
        var (output, _, _) = await RunAsync(line + "\r\nNOOP\r\n");

        Assert.IsFalse(output.EndsWith("200 ok\r\n", StringComparison.Ordinal));
    }

    private static async Task<(string Output, string Log, FakePassiveDataListenerOpener Opener)> RunAsync(string input, byte[]? upload = null)
    {
        var log = new ProtocolWireLog();
        var opener = new FakePassiveDataListenerOpener(upload ?? []);
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new FtpResponder(log, opener).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()), opener);
    }
}
