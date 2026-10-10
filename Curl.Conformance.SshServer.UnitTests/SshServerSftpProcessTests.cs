using System.Buffers.Binary;
using System.Text;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerSftpProcessTests
{
    private const byte Open = 3;
    private const byte Close = 4;
    private const byte Read = 5;
    private const byte Write = 6;
    private const byte LinkStat = 7;
    private const byte HandleStat = 8;
    private const byte SetStat = 9;
    private const byte HandleSetStat = 10;
    private const byte OpenDirectory = 11;
    private const byte ReadDirectory = 12;
    private const byte Remove = 13;
    private const byte MakeDirectory = 14;
    private const byte RemoveDirectory = 15;
    private const byte RealPath = 16;
    private const byte Stat = 17;
    private const byte Rename = 18;
    private const byte ReadLink = 19;

    [TestMethod]
    public void Answer_Init_IsVersionThreeWithNoExtensions()
    {
        using SshServerSftpProcess process = new("u");

        byte[] reply = process.Answer([1, 0, 0, 0, 3]);

        CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 0, 3 }, reply);
    }

    [TestMethod]
    public void Answer_UnimplementedRequest_IsOperationUnsupportedWithItsIdentifier()
    {
        using SshServerSftpProcess process = new("u");

        SshWireReader reply = new(process.Answer(Request(ReadLink, "/x")));

        Assert.AreEqual(101, reply.ReadByte());
        Assert.AreEqual(9u, reply.ReadUInt32());
        Assert.AreEqual(8u, reply.ReadUInt32());
        Assert.AreEqual("Operation unsupported", reply.ReadName());
    }

    [TestMethod]
    public void Answer_WriteThenReadAFile_StoresAndReturnsItsBytes()
    {
        using TemporaryFolder folder = new();
        string file = Path.Combine(folder.Path, "f.txt");
        using SshServerSftpProcess process = new("u");

        string writeHandle = HandleOf(process.Answer(Request(Open, file, 0x1Au, 0u)));
        uint written = StatusOf(process.Answer(Request(Write, Encoding.ASCII.GetBytes(writeHandle), 0UL, "hello")));
        SshWireReader attributes = Reply(process.Answer(Request(HandleStat, Encoding.ASCII.GetBytes(writeHandle))), 105);
        uint closed = StatusOf(process.Answer(Request(Close, Encoding.ASCII.GetBytes(writeHandle))));
        string readHandle = HandleOf(process.Answer(Request(Open, file, 0x01u, 0u)));
        SshWireReader data = Reply(process.Answer(Request(Read, Encoding.ASCII.GetBytes(readHandle), 1UL, 100u)), 103);
        uint end = StatusOf(process.Answer(Request(Read, Encoding.ASCII.GetBytes(readHandle), 5UL, 100u)));

        Assert.AreEqual(0u, written);
        Assert.AreEqual(0x0Fu, attributes.ReadUInt32());
        Assert.AreEqual(0u, attributes.ReadUInt32());
        Assert.AreEqual(5u, attributes.ReadUInt32());
        Assert.AreEqual(0u, closed);
        Assert.AreEqual("ello", Encoding.ASCII.GetString(data.ReadString().Span));
        Assert.AreEqual(1u, end);
        Assert.AreEqual("hello", File.ReadAllText(file));
    }

    [TestMethod]
    [DataRow(0x01u, "missing", 2u)]
    [DataRow(0x01u, "", 4u)]
    [DataRow(0x2Au, "existing", 4u)]
    public void Answer_OpenThatFails_IsItsStatus(uint flags, string name, uint expected)
    {
        using TemporaryFolder folder = new();
        folder.Write("existing", "x");
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(Open, Path.Combine(folder.Path, name), flags, 0u)));

        Assert.AreEqual(expected, status);
    }

    [TestMethod]
    [DataRow(0x0Au, "abcxyz")]
    [DataRow(0x0Bu, "abcxyz")]
    [DataRow(0x06u, "abcxyz")]
    public void Answer_OpenAnExistingFileToWriteWithoutTruncating_KeepsItsOtherBytes(uint flags, string expected)
    {
        using TemporaryFolder folder = new();
        string file = folder.Write("f", "abcdef");
        using SshServerSftpProcess process = new("u");

        string handle = HandleOf(process.Answer(Request(Open, file, flags, 0u)));
        process.Answer(Request(Write, Encoding.ASCII.GetBytes(handle), 3UL, "xyz"));
        process.Answer(Request(Close, Encoding.ASCII.GetBytes(handle)));

        Assert.AreEqual(expected, File.ReadAllText(file));
    }

    [TestMethod]
    [DataRow(Read)]
    [DataRow(Close)]
    [DataRow(ReadDirectory)]
    public void Answer_UnknownHandle_IsFailure(byte type)
    {
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(type, "99")));

        Assert.AreEqual(4u, status);
    }

    [TestMethod]
    public void Answer_ReadDirectory_ListsDotEntriesThenNamesInOrderThenEndOfFile()
    {
        using TemporaryFolder folder = new();
        folder.Write("b.txt", "abc");
        Directory.CreateDirectory(Path.Combine(folder.Path, "a"));
        using SshServerSftpProcess process = new("curltest");

        string handle = HandleOf(process.Answer(Request(OpenDirectory, folder.Path)));
        SshWireReader names = Reply(process.Answer(Request(ReadDirectory, Encoding.ASCII.GetBytes(handle))), 104);
        uint end = StatusOf(process.Answer(Request(ReadDirectory, Encoding.ASCII.GetBytes(handle))));
        uint closed = StatusOf(process.Answer(Request(Close, Encoding.ASCII.GetBytes(handle))));

        Assert.AreEqual(4u, names.ReadUInt32());
        List<string> listed = [];
        for (int entry = 0; entry < 4; entry++)
        {
            listed.Add(names.ReadName());
            listed.Add(names.ReadName()[..10]);
            names.ReadUInt32();
            names.ReadBytes(28);
        }

        CollectionAssert.AreEqual(new[] { ".", "drwxr-xr-x", "..", "drwxr-xr-x", "a", "drwxr-xr-x", "b.txt", "-rw-r--r--" }, listed);
        Assert.AreEqual(1u, end);
        Assert.AreEqual(0u, closed);
    }

    [TestMethod]
    [DataRow("f", 4u)]
    [DataRow("missing", 2u)]
    public void Answer_OpenDirectoryOnNoDirectory_IsItsStatus(string name, uint expected)
    {
        using TemporaryFolder folder = new();
        folder.Write("f", "x");
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(OpenDirectory, Path.Combine(folder.Path, name))));

        Assert.AreEqual(expected, status);
    }

    [TestMethod]
    [DataRow(Stat, "", 0x41EDu)]
    [DataRow(LinkStat, "f", 0x81A4u)]
    public void Answer_StatOfAPath_ReportsItsPermissions(byte type, string name, uint expected)
    {
        using TemporaryFolder folder = new();
        folder.Write("f", "x");
        using SshServerSftpProcess process = new("u");

        SshWireReader attributes = Reply(process.Answer(Request(type, Path.Combine(folder.Path, name))), 105);
        attributes.ReadBytes(20);

        Assert.AreEqual(expected, attributes.ReadUInt32());
    }

    [TestMethod]
    public void Answer_StatOfAMissingPath_IsNoSuchFile()
    {
        using TemporaryFolder folder = new();
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(Stat, Path.Combine(folder.Path, "missing"))));

        Assert.AreEqual(2u, status);
    }

    [TestMethod]
    public void Answer_SetStatOfAPathOrHandle_IsOk()
    {
        using TemporaryFolder folder = new();
        string file = folder.Write("f", "x");
        using SshServerSftpProcess process = new("u");

        uint byPath = StatusOf(process.Answer(Request(SetStat, file, 0u)));
        string handle = HandleOf(process.Answer(Request(Open, file, 0x01u, 0u)));
        uint byHandle = StatusOf(process.Answer(Request(HandleSetStat, Encoding.ASCII.GetBytes(handle), 0u)));

        Assert.AreEqual(0u, byPath);
        Assert.AreEqual(0u, byHandle);
    }

    [TestMethod]
    [DataRow("f", 0u)]
    [DataRow("missing", 2u)]
    [DataRow("d", 4u)]
    public void Answer_Remove_IsItsStatus(string name, uint expected)
    {
        using TemporaryFolder folder = new();
        folder.Write("f", "x");
        Directory.CreateDirectory(Path.Combine(folder.Path, "d"));
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(Remove, Path.Combine(folder.Path, name))));

        Assert.AreEqual(expected, status);
        Assert.IsFalse(File.Exists(Path.Combine(folder.Path, "f")) && expected == 0u);
    }

    [TestMethod]
    [DataRow("new", 0u)]
    [DataRow("f", 4u)]
    [DataRow("d", 4u)]
    [DataRow("missing/new", 2u)]
    public void Answer_MakeDirectory_IsItsStatus(string name, uint expected)
    {
        using TemporaryFolder folder = new();
        folder.Write("f", "x");
        Directory.CreateDirectory(Path.Combine(folder.Path, "d"));
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(MakeDirectory, Path.Combine(folder.Path, name))));

        Assert.AreEqual(expected, status);
        Assert.AreEqual(name is "new" or "d", Directory.Exists(Path.Combine(folder.Path, name)));
    }

    [TestMethod]
    [DataRow("d", 0u)]
    [DataRow("missing", 2u)]
    [DataRow("full", 4u)]
    public void Answer_RemoveDirectory_IsItsStatus(string name, uint expected)
    {
        using TemporaryFolder folder = new();
        Directory.CreateDirectory(Path.Combine(folder.Path, "d"));
        Directory.CreateDirectory(Path.Combine(folder.Path, "full"));
        folder.Write("full/f", "x");
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(RemoveDirectory, Path.Combine(folder.Path, name))));

        Assert.AreEqual(expected, status);
    }

    [TestMethod]
    [DataRow("f", 0u)]
    [DataRow("d", 0u)]
    [DataRow("missing", 2u)]
    public void Answer_Rename_IsItsStatus(string name, uint expected)
    {
        using TemporaryFolder folder = new();
        folder.Write("f", "x");
        Directory.CreateDirectory(Path.Combine(folder.Path, "d"));
        using SshServerSftpProcess process = new("u");

        uint status = StatusOf(process.Answer(Request(Rename, Path.Combine(folder.Path, name), Path.Combine(folder.Path, "renamed"))));

        Assert.AreEqual(expected, status);
        Assert.AreEqual(expected == 0u, Path.Exists(Path.Combine(folder.Path, "renamed")));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".")]
    public void Answer_RealPathOfTheCurrentDirectory_IsItsClientPath(string path)
    {
        using SshServerSftpProcess process = new("u");

        SshWireReader names = Reply(process.Answer(Request(RealPath, path)), 104);

        Assert.AreEqual(1u, names.ReadUInt32());
        Assert.AreEqual(SshServerSftpProcess.ClientPath(Environment.CurrentDirectory), names.ReadName());
    }

    [TestMethod]
    [DataRow("C:\\log\\f", "/C:/log/f")]
    [DataRow("/tmp/log", "/tmp/log")]
    public void ClientPath_LocalPath_IsSlashedAndRooted(string local, string expected)
    {
        string client = SshServerSftpProcess.ClientPath(local);

        Assert.AreEqual(expected, client);
    }

    [TestMethod]
    public void StatusFor_Exception_IsSftpServersVersionThreeCode()
    {
        uint[] codes =
        [
            SshServerSftpProcess.StatusFor(new FileNotFoundException()),
            SshServerSftpProcess.StatusFor(new DirectoryNotFoundException()),
            SshServerSftpProcess.StatusFor(new UnauthorizedAccessException()),
            SshServerSftpProcess.StatusFor(new IOException()),
        ];

        CollectionAssert.AreEqual(new uint[] { 2, 2, 3, 4 }, codes);
    }

    [TestMethod]
    public void LongName_RecentAndOldEntries_ShowTimeOrYear()
    {
        using TemporaryFolder folder = new();
        string file = folder.Write("f", "abc");
        DateTime modified = new(2020, 3, 4, 5, 6, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, modified);

        string recent = SshServerSftpProcess.LongName(file, "f", "curltest", modified.AddDays(1));
        string old = SshServerSftpProcess.LongName(folder.Path, "d", "me", modified.AddYears(1));

        Assert.AreEqual("-rw-r--r--   1 curltest curltest        3  Mar 4 05:06 f", recent);
        StringAssert.StartsWith(old, "drwxr-xr-x   1 me       me              0 ");
        StringAssert.EndsWith(old, " d");
    }

    [TestMethod]
    public async Task RunAsync_RequestsThenEof_AnswersEachFramedThenClosesWithStatusZero()
    {
        using TemporaryFolder folder = new();
        string file = folder.Write("f", "x");
        (SshServerSessionChannel channel, SshPacketWriter writer, SshPacketReader reader) = await OpenSubsystemAsync();

        Task<uint> process = SshServerSftpProcess.RunAsync(channel, CancellationToken.None).AsTask();
        await SendAsync(writer, [.. Framed([1, 0, 0, 0, 3]), .. Framed(Request(Open, file, 0x01u, 0u)), 0, 0]);
        await writer.WriteAsync(Header(SshConnectionMessageNumber.ChannelEof).ToArray(), CancellationToken.None);
        byte[] received = [.. await ReadDataAsync(reader), .. await ReadDataAsync(reader)];

        Assert.AreEqual(0u, await process);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 5, 2, 0, 0, 0, 3 }, received[..9]);
        Assert.AreEqual(102, received[13]);
    }

    private static async Task<(SshServerSessionChannel Channel, SshPacketWriter Writer, SshPacketReader Reader)> OpenSubsystemAsync()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());
        SshPacketWriter writer = new(client, new SystemSshRandomSource());
        SshPacketReader reader = new(new SshConnectionReader(client));
        SshWireWriter open = new();
        open.WriteByte(SshConnectionMessageNumber.ChannelOpen);
        open.WriteString("session"u8);
        open.WriteUInt32(0);
        open.WriteUInt32(1 << 20);
        open.WriteUInt32(32768);
        await writer.WriteAsync(open.ToArray(), CancellationToken.None);
        SshWireWriter request = Header(SshConnectionMessageNumber.ChannelRequest);
        request.WriteString("subsystem"u8);
        request.WriteBoolean(false);
        request.WriteString("sftp"u8);
        await writer.WriteAsync(request.ToArray(), CancellationToken.None);
        SshServerSessionChannel channel = await SshServerSessionChannel.OpenAsync(transport, "someone", CancellationToken.None);
        await reader.ReadAsync(CancellationToken.None);
        return (channel, writer, reader);
    }

    private static async Task SendAsync(SshPacketWriter writer, byte[] bytes)
    {
        SshWireWriter data = Header(SshConnectionMessageNumber.ChannelData);
        data.WriteString(bytes);
        await writer.WriteAsync(data.ToArray(), CancellationToken.None);
    }

    private static async Task<byte[]> ReadDataAsync(SshPacketReader reader)
    {
        SshWireReader message = new(await reader.ReadAsync(CancellationToken.None));
        Assert.AreEqual(SshConnectionMessageNumber.ChannelData, message.ReadByte());
        message.ReadUInt32();
        return message.ReadString().ToArray();
    }

    private static SshWireWriter Header(byte messageNumber)
    {
        SshWireWriter writer = new();
        writer.WriteByte(messageNumber);
        writer.WriteUInt32(0);
        return writer;
    }

    private static byte[] Framed(byte[] packet)
    {
        byte[] framed = new byte[packet.Length + 4];
        BinaryPrimitives.WriteUInt32BigEndian(framed, (uint)packet.Length);
        packet.CopyTo(framed, 4);
        return framed;
    }

    // A request with identifier 9: strings and byte arrays as SSH strings, uint as uint32, ulong as uint64.
    private static byte[] Request(byte type, params object[] fields)
    {
        SshWireWriter request = new();
        request.WriteByte(type);
        request.WriteUInt32(9);
        foreach (object field in fields)
        {
            switch (field)
            {
                case string text:
                    request.WriteString(Encoding.UTF8.GetBytes(text));
                    break;
                case byte[] bytes:
                    request.WriteString(bytes);
                    break;
                case uint value:
                    request.WriteUInt32(value);
                    break;
                case ulong value:
                    request.WriteUInt32((uint)(value >> 32));
                    request.WriteUInt32((uint)value);
                    break;
            }
        }

        return request.ToArray();
    }

    private static SshWireReader Reply(byte[] reply, byte type)
    {
        SshWireReader reader = new(reply);
        Assert.AreEqual(type, reader.ReadByte(), $"status {(reply[0] == 101 ? BinaryPrimitives.ReadUInt32BigEndian(reply.AsSpan(5)) : 0)}");
        Assert.AreEqual(9u, reader.ReadUInt32());
        return reader;
    }

    private static uint StatusOf(byte[] reply) => Reply(reply, 101).ReadUInt32();

    private static string HandleOf(byte[] reply) => Encoding.ASCII.GetString(Reply(reply, 102).ReadString().Span);

    private sealed class TemporaryFolder : IDisposable
    {
        internal string Path { get; } = Directory.CreateTempSubdirectory("sftp-").FullName;

        internal string Write(string name, string contents)
        {
            string file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, contents);
            return file;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
