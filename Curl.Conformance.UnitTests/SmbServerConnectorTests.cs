using System.Buffers.Binary;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smb;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="SmbServerConnector"/> and its <see cref="SmbServerConnection"/>, driven by Curl's own SMB client
/// (<c>Curl.Original.Protocol.Smb.UnitLibrary</c>'s handler) and by hand-made messages for what curl never sends.
/// </summary>
[TestClass]
public sealed class SmbServerConnectorTests
{
    private const string Hello = "<reply>\n<data>\nBasic SMB test complete\n</data>\n</reply>\n";

    private const string Url = "smb://127.0.0.1:9020/TESTS/1451";

    [TestMethod]
    public async Task Download_BySmbHandler_GetsTheReplyData()
    {
        (int exitCode, string output, string error) = await RunSmbAsync(Hello, null, Url);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual("Basic SMB test complete\n", output);
    }

    [TestMethod]
    public async Task TwoDownloads_BySmbHandler_GetTheReplyDataTwice()
    {
        (int exitCode, string output, string error) = await RunSmbAsync(Hello, null, Url, Url);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual("Basic SMB test complete\nBasic SMB test complete\n", output);
    }

    [TestMethod]
    public async Task Download_CaseWithoutReplyData_GetsAnEmptyFile()
    {
        (int exitCode, string output, string error) = await RunSmbAsync("<reply>\n</reply>\n", null, Url);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public async Task Download_FromAnotherShare_IsRefusedAsRemoteFileNotFound()
    {
        (int exitCode, _, _) = await RunSmbAsync(Hello, null, "smb://127.0.0.1:9020/OTHER/1451");

        Assert.AreEqual(78, exitCode);
    }

    [TestMethod]
    public async Task Upload_BySmbHandler_IsRefusedAsNotImplemented()
    {
        using MemoryStream source = new("upload"u8.ToArray());

        (int exitCode, _, _) = await RunSmbAsync(Hello, source, Url);

        Assert.AreEqual(25, exitCode);
    }

    [TestMethod]
    public async Task Negotiate_WrittenInPieces_IsAnsweredOnceWhole()
    {
        IConnection connection = await ConnectAsync(Hello);
        byte[] negotiate = Message(0x72, [0x00, 0x00, 0x00]);

        ValueTask<byte[]> reply = ReadMessageAsync(connection);
        await connection.WriteAsync(negotiate.AsMemory(0, 2), CancellationToken.None);
        await connection.WriteAsync(negotiate.AsMemory(2, 10), CancellationToken.None);
        await connection.WriteAsync(negotiate.AsMemory(12), CancellationToken.None);
        byte[] answer = await reply;

        Assert.AreEqual(0x72, answer[8]);
        Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(answer.AsSpan(9)));
        Assert.AreEqual(0x98, answer[13]);
        Assert.AreEqual("0123456789abcdef", Convert.ToHexStringLower(answer.AsSpan(73, 8)));
    }

    [TestMethod]
    [DataRow((byte)0x2f, DisplayName = "write")]
    [DataRow((byte)0x08, DisplayName = "query information")]
    public async Task OtherCommand_IsAnsweredNotImplemented(byte command)
    {
        IConnection connection = await ConnectAsync(Hello);

        await connection.WriteAsync(Message(command, [0x00, 0x00, 0x00]), CancellationToken.None);
        byte[] answer = await ReadMessageAsync(connection);

        Assert.AreEqual(SmbServerConnection.NotImplemented, BinaryPrimitives.ReadUInt32LittleEndian(answer.AsSpan(9)));
    }

    [TestMethod]
    public async Task MessageShorterThanAnSmbHeader_IsAnsweredNotImplemented()
    {
        IConnection connection = await ConnectAsync(Hello);

        await connection.WriteAsync(new byte[] { 0x00, 0x00, 0x00, 0x04, 0xff, (byte)'S', (byte)'M', (byte)'B' }, CancellationToken.None);
        byte[] answer = await ReadMessageAsync(connection);

        Assert.AreEqual(SmbServerConnection.NotImplemented, BinaryPrimitives.ReadUInt32LittleEndian(answer.AsSpan(9)));
    }

    [TestMethod]
    public async Task Connection_IsPlainLoopbackAndDisposes()
    {
        IConnection connection = await ConnectAsync(Hello);

        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual("127.0.0.1:9020", connection.RemoteEndPoint?.ToString());
        Assert.IsNull(connection.LocalEndPoint);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task OtherPort_ReachesTheWrappedServer()
    {
        RecordingConnector backend = new();
        SmbServerConnector smb = new(ParsedTestCase.From(Hello), backend);

        ConnectResult result = await smb.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None);

        Assert.AreEqual(8990, backend.Target?.Port);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Screening_SmbCase_IsNotSkippedForItsServer()
    {
        string sections = "<client>\n<server>\nsmb\n</server>\n<features>\nsmb\n</features>\n<command>\n" + Url + "\n</command>\n</client>\n";

        string? reason = UpstreamCaseScreening.FindSkipReason(new(ReadOnlyMemory<byte>.Empty, [], [], null), ParsedTestCase.From(sections), new HashSet<string>(["smb"]));

        Assert.IsNull(reason, reason);
    }

    private static async Task<IConnection> ConnectAsync(string sections)
    {
        SmbServerConnector smb = new(ParsedTestCase.From(sections), new RecordingConnector());
        ConnectResult result = await smb.ConnectAsync(new ConnectTarget("127.0.0.1", SmbServerConnector.SmbPort, false), CancellationToken.None);
        return result.Connection!;
    }

    // A whole message: the NetBIOS header, an SMB header of the command and the body.
    private static byte[] Message(byte command, byte[] body)
    {
        byte[] message = new byte[36 + body.Length];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2), (ushort)(message.Length - 4));
        message[4] = 0xff;
        message[5] = (byte)'S';
        message[6] = (byte)'M';
        message[7] = (byte)'B';
        message[8] = command;
        body.CopyTo(message, 36);
        return message;
    }

    private static async ValueTask<byte[]> ReadMessageAsync(IConnection connection)
    {
        List<byte> received = [];
        byte[] buffer = new byte[256];
        while (received.Count < 4 || received.Count < 4 + BinaryPrimitives.ReadUInt16BigEndian(received.ToArray().AsSpan(2)))
        {
            int read = await connection.ReadAsync(buffer, CancellationToken.None);
            received.AddRange(buffer.AsSpan(0, read));
        }

        return [.. received];
    }

    // Runs each URL through Curl's SMB handler as user curltest, as curl -u curltest:curltest did before
    // curl 8.22.0's reference builds turned smb off (ADR-0835), stopping at the first failure.
    private static async Task<(int ExitCode, string Output, string Error)> RunSmbAsync(string sections, Stream? upload, params string[] urls)
    {
        SmbProtocolHandler handler = new(new SmbServerConnector(ParsedTestCase.From(sections), new RecordingConnector()));
        using MemoryStream output = new();
        TransferResult result = TransferResult.Success(0);
        foreach (string url in urls.TakeWhile(_ => result.ExitCode == CurlExitCode.Ok))
        {
            result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = output, Upload = upload, Credentials = new("curltest", "curltest") });
        }

        return ((int)result.ExitCode, Encoding.Latin1.GetString(output.ToArray()), result.ErrorMessage ?? string.Empty);
    }

    // Refuses every connection and remembers where the last one was to go.
    private sealed class RecordingConnector : IConnector
    {
        public ConnectTarget? Target { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Target = target;
            return ValueTask.FromResult(ConnectResult.Refused("refused"));
        }
    }
}
