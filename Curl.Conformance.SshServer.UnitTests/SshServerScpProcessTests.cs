using System.Text;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerScpProcessTests
{
    private const uint ClientChannel = 7;

    [TestMethod]
    public async Task RunAsync_SourceOfAFile_SendsTimesFileLineAndBytesThenExitsZero()
    {
        using TemporaryFolder folder = new();
        string file = folder.Write("f.txt", "hello");
        Session session = await Session.OpenAsync($"scp -pf '{file}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("\0");
        string times = await session.ReadTextAsync();
        await session.SendAsync("\0");
        string fileLine = await session.ReadTextAsync();
        await session.SendAsync("\0");
        string contents = await session.ReadTextAsync();
        await session.SendEofAsync();

        Assert.AreEqual(0u, await process);
        StringAssert.StartsWith(times, "T");
        StringAssert.EndsWith(times, " 0\n");
        Assert.AreEqual("C0644 5 f.txt\n", fileLine);
        Assert.AreEqual("hello\0", contents);
        Assert.AreEqual(0u, await session.ReadExitStatusAsync());
    }

    [TestMethod]
    [DataRow("missing", "No such file or directory")]
    [DataRow("", "not a regular file")]
    public async Task RunAsync_SourceOfNoFile_SendsTheErrorLineAndExitsOne(string name, string message)
    {
        using TemporaryFolder folder = new();
        string path = Path.Combine(folder.Path, name);
        Session session = await Session.OpenAsync($"scp -f '{path}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("\0");

        Assert.AreEqual($"\u0001scp: {path}: {message}\n", await session.ReadTextAsync());
        Assert.AreEqual(1u, await process);
    }

    [TestMethod]
    public async Task RunAsync_SourceWhoseClientEndsAtOnce_ExitsZero()
    {
        Session session = await Session.OpenAsync("scp -f /none");

        Task<uint> process = session.RunAsync();
        await session.SendEofAsync();

        Assert.AreEqual(0u, await process);
    }

    [TestMethod]
    public async Task RunAsync_SinkToAFilePath_AcknowledgesEachStepAndWritesTheFile()
    {
        using TemporaryFolder folder = new();
        string target = Path.Combine(folder.Path, "upload.1");
        Session session = await Session.OpenAsync($"scp -t '{target}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("T1 0 1 0\nC0644 3 ignored\nab");
        await session.SendAsync("c\0");
        await session.SendEofAsync();

        Assert.AreEqual(0u, await process);
        Assert.AreEqual("\0\0\0\0", await session.ReadTextAsync(4));
        Assert.AreEqual("abc", File.ReadAllText(target));
    }

    [TestMethod]
    public async Task RunAsync_SinkIntoADirectoryWithDAndELines_WritesTheFileInTheNewDirectory()
    {
        using TemporaryFolder folder = new();
        Session session = await Session.OpenAsync($"scp -r -t '{folder.Path}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("D0755 0 sub\nC0644 1 y.txt\nz\0E\nC0644 1 top.txt\nq\0\n");

        Assert.AreEqual(0u, await process);
        Assert.AreEqual("z", File.ReadAllText(Path.Combine(folder.Path, "sub", "y.txt")));
        Assert.AreEqual("q", File.ReadAllText(Path.Combine(folder.Path, "top.txt")));
    }

    [TestMethod]
    public async Task RunAsync_SinkToANewPathGivenADLine_MakesTheDirectoryUnderThePath()
    {
        using TemporaryFolder folder = new();
        string target = Path.Combine(folder.Path, "new");
        Session session = await Session.OpenAsync($"scp -r -t '{target}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("D0755 0 sub\n");
        await session.SendEofAsync();

        Assert.AreEqual(0u, await process);
        Assert.IsTrue(Directory.Exists(Path.Combine(target, "sub")));
    }

    [TestMethod]
    public async Task RunAsync_SinkWhoseFolderIsMissing_SendsTheErrorLineAndExitsOne()
    {
        using TemporaryFolder folder = new();
        string target = Path.Combine(folder.Path, "no-folder", "file");
        Session session = await Session.OpenAsync($"scp -t '{target}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("C0644 1 file\n");
        await session.SendEofAsync();

        Assert.AreEqual(1u, await process);
        string expected = $"\0\u0001scp: {target}: No such file or directory\n";
        Assert.AreEqual(expected, await session.ReadTextAsync(expected.Length));
    }

    [TestMethod]
    public async Task RunAsync_SinkGivenAnUnknownLine_SendsAProtocolErrorAndExitsOne()
    {
        Session session = await Session.OpenAsync("scp -t /none");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("X1\n");

        Assert.AreEqual(1u, await process);
        string expected = "\0\u0001scp: protocol error: unexpected <X1>\n";
        Assert.AreEqual(expected, await session.ReadTextAsync(expected.Length));
    }

    [TestMethod]
    public async Task RunAsync_SinkWhoseClientEndsAfterTheBytes_WritesTheFileAndExitsZero()
    {
        using TemporaryFolder folder = new();
        string target = Path.Combine(folder.Path, "upload.2");
        Session session = await Session.OpenAsync($"scp -t '{target}'");

        Task<uint> process = session.RunAsync();
        await session.SendAsync("C0644 2 x\nok");
        await session.SendEofAsync();

        Assert.AreEqual(0u, await process);
        Assert.AreEqual("ok", File.ReadAllText(target));
    }

    [TestMethod]
    [DataRow("/C:/log/file", "C:/log/file")]
    [DataRow("//z:\\log", "z:\\log")]
    [DataRow("//tmp/log/file", "//tmp/log/file")]
    [DataRow("/1:/x", "/1:/x")]
    [DataRow("/", "/")]
    public void LocalPath_ClientPath_DropsTheSlashesBeforeADriveOnly(string path, string expected)
    {
        string local = SshServerScpProcess.LocalPath(path);

        Assert.AreEqual(expected, local);
    }

    private sealed class TemporaryFolder : IDisposable
    {
        internal string Path { get; } = Directory.CreateTempSubdirectory("scp-").FullName;

        internal string Write(string name, string contents)
        {
            string file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, contents);
            return file;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed record Session(SshServerSessionChannel Channel, SshPacketWriter Client, SshPacketReader ClientReader, string Command)
    {
        internal static async Task<Session> OpenAsync(string command)
        {
            (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
            SshServerTransport transport = new(server, new SystemSshRandomSource());
            SshPacketWriter writer = new(client, new SystemSshRandomSource());
            SshPacketReader reader = new(new SshConnectionReader(client));
            SshWireWriter open = new();
            open.WriteByte(SshConnectionMessageNumber.ChannelOpen);
            open.WriteString("session"u8);
            open.WriteUInt32(ClientChannel);
            open.WriteUInt32(1 << 20);
            open.WriteUInt32(32768);
            await writer.WriteAsync(open.ToArray(), CancellationToken.None);
            SshWireWriter request = Header(SshConnectionMessageNumber.ChannelRequest);
            request.WriteString("exec"u8);
            request.WriteBoolean(false);
            request.WriteString(Encoding.UTF8.GetBytes(command));
            await writer.WriteAsync(request.ToArray(), CancellationToken.None);
            SshServerSessionChannel channel = await SshServerSessionChannel.OpenAsync(transport, "someone", CancellationToken.None);
            await reader.ReadAsync(CancellationToken.None);
            return new Session(channel, writer, reader, command);
        }

        internal Task<uint> RunAsync() =>
            SshServerScpProcess.RunAsync(Channel, SshServerScpCommand.Parse(Command)!, CancellationToken.None).AsTask();

        internal async Task SendAsync(string text)
        {
            SshWireWriter data = Header(SshConnectionMessageNumber.ChannelData);
            data.WriteString(Encoding.UTF8.GetBytes(text));
            await Client.WriteAsync(data.ToArray(), CancellationToken.None);
        }

        internal async Task SendEofAsync() =>
            await Client.WriteAsync(Header(SshConnectionMessageNumber.ChannelEof).ToArray(), CancellationToken.None);

        // Reads one CHANNEL_DATA message, or, given a count, as many as carry that many bytes.
        internal async Task<string> ReadTextAsync(int count = 0)
        {
            StringBuilder text = new();
            do
            {
                SshWireReader message = new(await ClientReader.ReadAsync(CancellationToken.None));
                Assert.AreEqual(SshConnectionMessageNumber.ChannelData, message.ReadByte());
                message.ReadUInt32();
                text.Append(Encoding.UTF8.GetString(message.ReadString().Span));
            }
            while (text.Length < count);

            return text.ToString();
        }

        internal async Task<uint> ReadExitStatusAsync()
        {
            SshWireReader status = new(await ClientReader.ReadAsync(CancellationToken.None));
            Assert.AreEqual(SshConnectionMessageNumber.ChannelRequest, status.ReadByte());
            status.ReadUInt32();
            Assert.AreEqual("exit-status", status.ReadName());
            status.ReadBoolean();
            return status.ReadUInt32();
        }

        private static SshWireWriter Header(byte messageNumber)
        {
            SshWireWriter writer = new();
            writer.WriteByte(messageNumber);
            writer.WriteUInt32(0);
            return writer;
        }
    }
}
