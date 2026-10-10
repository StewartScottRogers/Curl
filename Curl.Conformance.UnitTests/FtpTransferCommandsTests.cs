using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="FtpTransferCommands"/> answers PASV, EPSV, RETR, LIST, NLST, SIZE, MDTM and REST as ftpserver.pl does.</summary>
[TestClass]
public sealed class FtpTransferCommandsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("", "PASV", "227 Entering Passive Mode (127,0,0,1,35,45)\r\n")]
    [DataRow("<servercmd>\nPASVBADIP\n</servercmd>\n", "PASV", "227 Entering Passive Mode (1,2,3,4,35,45)\r\n")]
    [DataRow("", "EPSV", "229 Entering Passive Mode (|||9005|)\r\n")]
    public void TryAnswer_PassiveCommand_OpensADataConnectionAndNamesItsPort(string reply, string command, string expected)
    {
        List<FtpDataConnection> opened = [];
        FtpTransferCommands commands = Create(reply, opened);

        string answer = Answer(commands, command, string.Empty);

        Assert.AreEqual(expected, answer);
        Assert.HasCount(1, opened);
    }

    [TestMethod]
    [DataRow("<data>\nhello\n</data>\n", "1", "150 Binary data connection for 1 () (6 bytes).\r\n226 File transfer complete\r\n", "hello\n")]
    [DataRow("<data2>\npart\n</data2>\n", "/dir/20002", "150 Binary data connection for 2 (2) (5 bytes).\r\n226 File transfer complete\r\n", "part\n")]
    [DataRow("<data>\nhello\n</data>\n<servercmd>\nRETRWEIRDO\n</servercmd>\n", "1", "150 Binary data connection for 1 () (6 bytes).\r\n226 File transfer complete\r\n", "hello\n")]
    [DataRow("<data>\nhello\n</data>\n<servercmd>\nRETRNOSIZE\n</servercmd>\n", "1", "150 Binary data connection for 1 () size?.\r\n226 File transfer complete\r\n", "hello\n")]
    [DataRow("<data>\nhello\n</data>\n<servercmd>\nRETRSIZE 99\n</servercmd>\n", "1", "150 Binary data connection for 1 () (99 bytes).\r\n226 File transfer complete\r\n", "hello\n")]
    [DataRow("<data sendzero=\"yes\">\n</data>\n", "1", "150 Binary data connection for 1 () (0 bytes).\r\n226 File transfer complete\r\n", "")]
    public async Task TryAnswer_RetrAfterPasv_SendsTheDataAndClosesTheDataConnection(string reply, string argument, string expected, string expectedData)
    {
        List<FtpDataConnection> opened = [];
        FtpTransferCommands commands = Create(reply, opened);
        Answer(commands, "PASV", string.Empty);

        string answer = Answer(commands, "RETR", argument);

        Assert.AreEqual(expected, answer);
        Assert.AreEqual(expectedData, await ReadToEndAsync(opened[0]));
    }

    [TestMethod]
    [DataRow("<data>\nhello\n</data>\n", "file", "550 : No such file or directory.\r\n")]
    [DataRow("<data>\nhello\n</data>\n", "1x", "550 1x: No such file or directory.\r\n")]
    [DataRow("", "1", "550 1: No such file or directory.\r\n")]
    [DataRow("<data>\n</data>\n", "1", "550 1: No such file or directory.\r\n")]
    public void TryAnswer_RetrOfNoData_AnswersNoSuchFile(string reply, string argument, string expected)
    {
        FtpTransferCommands commands = Create(reply, []);
        Answer(commands, "EPSV", string.Empty);

        Assert.AreEqual(expected, Answer(commands, "RETR", argument));
    }

    [TestMethod]
    public void TryAnswer_RestThenRetr_TakesTheOffsetOffTheSizeOnce()
    {
        FtpTransferCommands commands = Create("<data>\nhello\n</data>\n", []);
        Answer(commands, "PASV", string.Empty);

        string rest = Answer(commands, "REST", " 4");
        string first = Answer(commands, "RETR", "1");
        Answer(commands, "PASV", string.Empty);
        string second = Answer(commands, "RETR", "1");

        Assert.AreEqual(string.Empty, rest);
        Assert.StartsWith("150 Binary data connection for 1 () (2 bytes).", first);
        Assert.StartsWith("150 Binary data connection for 1 () (6 bytes).", second);
    }

    [TestMethod]
    [DataRow("RETR")]
    [DataRow("LIST")]
    [DataRow("NLST")]
    public void TryAnswer_TransferWithoutPasv_SendsNothing(string command)
    {
        FtpTransferCommands commands = Create("<data>\nhello\n</data>\n", []);

        Assert.AreEqual(string.Empty, Answer(commands, command, "1"));
    }

    [TestMethod]
    [DataRow("<data>\na\r\nb\n</data>\n", "LIST", "a\r\nb\r\n")]
    [DataRow("", "LIST", "")]
    [DataRow("", "NLST", "file\r\nwith space\r\nfake\r\n..\r\n ..\r\nfunny\r\nREADME\r\n")]
    public async Task TryAnswer_ListingAfterPasv_SendsTheListing(string reply, string command, string expectedData)
    {
        List<FtpDataConnection> opened = [];
        FtpTransferCommands commands = Create(reply, opened);
        Answer(commands, "PASV", string.Empty);

        string answer = Answer(commands, command, string.Empty);

        Assert.AreEqual("226 ASCII transfer complete\r\n", answer);
        Assert.AreEqual(expectedData, await ReadToEndAsync(opened[0]));
    }

    [TestMethod]
    [DataRow("<data>\nhello\n</data>\n", "/a/1/", "213 6\r\n")]
    [DataRow("", "1", "550 1: No such file or directory.\r\n")]
    [DataRow("<size>\n42\n</size>\n", "20003", "213 42\r\n")]
    [DataRow("<size>\n-1\n</size>\n", "1", "550 1: No such file or directory.\r\n")]
    [DataRow("<data>\nhello\n</data>\n", "file", "")]
    public void TryAnswer_Size_AnswersFromTheSizeOrDataPart(string reply, string argument, string expected)
    {
        Assert.AreEqual(expected, Answer(Create(reply, []), "SIZE", argument));
    }

    [TestMethod]
    [DataRow("<mdtm>\n213 20090213134353\n</mdtm>\n", "1", "213 20090213134353\r\n")]
    [DataRow("<mdtm>\n-1\n</mdtm>\n", "20001", "550 2: no such file.\r\n")]
    [DataRow("<mdtm>\n0\n</mdtm>\n", "1", "500 MDTM: no such command.\r\n")]
    [DataRow("<mdtm>\n-x\n</mdtm>\n", "1", "-x\r\n")]
    [DataRow("<mdtm>\n-0\n</mdtm>\n", "1", "-0\r\n")]
    [DataRow("<mdtm>\n-\n</mdtm>\n", "1", "-\r\n")]
    [DataRow("<mdtm>\n213 1\n</mdtm>\n", "file", "500 MDTM: no such command.\r\n")]
    public void TryAnswer_Mdtm_AnswersFromTheMdtmPart(string reply, string argument, string expected)
    {
        Assert.AreEqual(expected, Answer(Create(reply, []), "MDTM", argument));
    }

    [TestMethod]
    public void TryAnswer_OtherCommand_IsNotATransferCommand()
    {
        bool answered = Create(string.Empty, []).TryAnswer("STOR", "1", out string answer);

        Assert.IsFalse(answered);
        Assert.AreEqual(string.Empty, answer);
    }

    [TestMethod]
    public async Task DataConnection_ReadBeforeSend_WaitsForTheData()
    {
        FtpDataConnection connection = new();
        byte[] buffer = new byte[8];

        ValueTask<int> read = connection.ReadAsync(buffer, TestContext.CancellationToken);
        bool waited = !read.IsCompleted;
        connection.Send("ab"u8);
        int count = await read;
        connection.Close();
        await connection.WriteAsync(new byte[] { 1 }, TestContext.CancellationToken);
        await connection.FlushAsync(TestContext.CancellationToken);
        int afterClose = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        await connection.DisposeAsync();

        Assert.IsTrue(waited);
        Assert.AreEqual(2, count);
        Assert.AreEqual(0, afterClose);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsNull(connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_PassivePort_ReachesTheDataConnectionOnceThenTheWrappedServer()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<data>\nhi\n</data>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));
        ConnectResult control = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.FtpPort, false), TestContext.CancellationToken);
        await control.Connection!.WriteAsync(Encoding.Latin1.GetBytes("PASV\r\nRETR 1\r\n"), TestContext.CancellationToken);

        ConnectResult data = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.PassivePort, false), TestContext.CancellationToken);
        ConnectResult again = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);
        ConnectResult passiveAgain = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.PassivePort, false), TestContext.CancellationToken);

        Assert.AreEqual("hi\n", await ReadToEndAsync(data.Connection!));
        Assert.IsTrue(again.IsConnectionRefused);
        Assert.IsInstanceOfType<SwsHttpServerConnection>(passiveAgain.Connection);
    }

    [TestMethod]
    [DataRow("LIST", "150 here comes a directory\r\n")]
    [DataRow("SIZE 1", "550 1: No such file or directory.\r\n")]
    [DataRow("STOR 1", "500 STOR is not dealt with!\r\n")]
    public void Responder_WithTransferCommands_AnswersAfterTheDisplayText(string line, string expected)
    {
        FtpControlChannelResponder responder = new(LineProtocolServerCommands.Read([]), Create(string.Empty, []));

        LineProtocolReply reply = responder.Answer(line);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(reply.Bytes.Span));
    }

    private static FtpTransferCommands Create(string reply, List<FtpDataConnection> opened) =>
        new(ParsedTestCase.From($"<reply>\n{reply}</reply>\n"), opened.Add);

    private static string Answer(FtpTransferCommands commands, string command, string argument)
    {
        Assert.IsTrue(commands.TryAnswer(command, argument, out string answer));
        return answer;
    }

    private async Task<string> ReadToEndAsync(IConnection connection)
    {
        List<byte> bytes = [];
        byte[] buffer = new byte[64];
        int count;
        while ((count = await connection.ReadAsync(buffer, TestContext.CancellationToken)) > 0)
        {
            bytes.AddRange(buffer.AsSpan(0, count));
        }

        return Encoding.Latin1.GetString([.. bytes]);
    }
}
