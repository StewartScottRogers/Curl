using System.Buffers.Binary;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Adversarial black-box tests (BL-1516, by <c>Documentation/Wiki/Adversarial-Testing.md</c>):
/// the SMB parsers and the handler at their length boundaries, with frames one field wrong,
/// in the URL and user-name partitions curl 8.21.0's <c>smb.c</c> treats specially, and
/// under repeated and concurrent use. Every refusal is the one the library names; nothing
/// throws or hangs.
/// </summary>
[TestClass]
public sealed class SmbAdversarialTests
{
    // Where curl's read response puts its data length, byte count and data offset words.
    private const int ReadDataLengthOffset = SmbMessageHeader.Length + 11;
    private const int ReadDataOffsetOffset = SmbMessageHeader.Length + 13;
    private const int ReadByteCountOffset = SmbMessageHeader.Length + 25;
    private const int ReadDataStart = SmbMessageHeader.Length + 27;

    private static readonly NetworkCredential User = new("User", "Password");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // ---- Boundaries ----

    [TestMethod]
    [DataRow(80, false)]
    [DataRow(81, true)]
    public void NegotiateResponse_LengthAroundTheChallengesEnd_IsReadOnlyWhenWhole(int length, bool expected)
    {
        byte[] received = SmbRecordedExchange.NegotiateResponse.AsSpan(0, Math.Min(length, SmbRecordedExchange.NegotiateResponse.Length)).ToArray();
        Array.Resize(ref received, length);
        Diagnostics.Arrange("length", length);

        bool read = SmbNegotiateResponse.TryRead(received, out SmbNegotiateResponse? response);

        Diagnostics.Assert("read", expected, read);
        Assert.AreEqual(expected, read);
        Assert.AreEqual(expected, response is not null);
    }

    [TestMethod]
    [DataRow(SmbOpenResponse.Length - 1, false)]
    [DataRow(SmbOpenResponse.Length, true)]
    public void OpenResponse_LengthAroundItsFixedSize_IsReadOnlyWhenWhole(int length, bool expected)
    {
        byte[] received = SmbRecordedExchange.OpenAccepted[..length];
        Diagnostics.Arrange("length", length);

        bool read = SmbOpenResponse.TryRead(received, out SmbOpenResponse? response);

        Diagnostics.Assert("read", expected, read);
        Assert.AreEqual(expected, read);
        Assert.AreEqual(expected, response is not null);
    }

    [TestMethod]
    [DataRow(long.MaxValue)]
    [DataRow(long.MinValue)]
    [DataRow(0L)]
    [DataRow(-1L)]
    public void OpenResponse_ExtremeLastChangeTime_IsClampedNotThrown(long fileTime)
    {
        byte[] received = SmbRecordedExchange.OpenAccepted;
        BinaryPrimitives.WriteInt64LittleEndian(received.AsSpan(72), fileTime);
        Diagnostics.Arrange("file time", fileTime);

        Assert.IsTrue(SmbOpenResponse.TryRead(received, out SmbOpenResponse? response));

        DateTimeOffset expected = fileTime == long.MaxValue
            ? DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            : DateTimeOffset.UnixEpoch;
        Diagnostics.Assert("last change", expected, response!.LastChangeTimeUtc);
        Assert.AreEqual(expected, response.LastChangeTimeUtc);
    }

    // 50 bytes ends inside the data offset word: refused as short, not read past the end (BL-1665).
    [TestMethod]
    [DataRow(SmbMessageHeader.Length + 13, "Failure when receiving data from the peer")]
    [DataRow(SmbMessageHeader.Length + 14, "Failure when receiving data from the peer")]
    [DataRow(SmbMessageHeader.Length + 15, "Invalid input packet")]
    public void ReadResponse_LengthAroundTheDataOffsetWord_IsRefusedAsShortOrAsDataPastTheEnd(int length, string expected)
    {
        byte[] received = SmbRecordedExchange.ReadAccepted[..length];
        Diagnostics.Arrange("length", length);

        string? refusal = SmbReadResponse.TryGetData(received, out _);

        Diagnostics.Assert("refusal", expected, refusal);
        Assert.AreEqual(expected, refusal);
    }

    // 42 bytes ends inside the count word: refused, not read past the end (BL-1665).
    [TestMethod]
    [DataRow(SmbMessageHeader.Length + 5, false)]
    [DataRow(SmbMessageHeader.Length + 6, false)]
    [DataRow(SmbMessageHeader.Length + 7, true)]
    public void WriteResponse_LengthAroundTheCountWord_IsReadOnlyWhenWhole(int length, bool expected)
    {
        byte[] received = SmbRecordedExchange.WriteAccepted(11)[..length];
        Diagnostics.Arrange("length", length);

        bool read = SmbWriteResponse.TryReadCount(received, out int count);

        Diagnostics.Assert("read", expected, read);
        Assert.AreEqual(expected, read);
        Assert.AreEqual(expected ? 11 : 0, count);
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, "Invalid input packet")]
    public void ReadResponse_DataEndingAtOrOnePastTheMessage_IsTakenOrRefused(int pastTheEnd, string? expected)
    {
        byte[] received = ReadReply(11);
        BinaryPrimitives.WriteUInt16LittleEndian(received.AsSpan(ReadDataLengthOffset), (ushort)(11 + pastTheEnd));
        Diagnostics.Arrange("data past the message", pastTheEnd);

        string? refusal = SmbReadResponse.TryGetData(received, out ReadOnlyMemory<byte> data);

        Diagnostics.Assert("refusal", expected, refusal);
        Assert.AreEqual(expected, refusal);
        Assert.AreEqual(expected is null ? 11 : 0, data.Length);
    }

    [TestMethod]
    public void ReadResponse_LargestDataOffsetAndLength_IsRefusedWithoutOverflow()
    {
        byte[] received = ReadReply(11);
        BinaryPrimitives.WriteUInt16LittleEndian(received.AsSpan(ReadDataLengthOffset), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(received.AsSpan(ReadDataOffsetOffset), ushort.MaxValue);
        Diagnostics.Arrange("data offset and length", "0xffff, 0xffff");

        string? refusal = SmbReadResponse.TryGetData(received, out ReadOnlyMemory<byte> data);

        Diagnostics.Assert("refusal", "Invalid input packet", refusal);
        Assert.AreEqual("Invalid input packet", refusal);
        Assert.IsTrue(data.IsEmpty);
    }

    [TestMethod]
    public async Task ReceiveAsync_LargestNetBiosLength_IsRefusedAsTooLarge()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.Hex("00 00 ff ff"));
        Diagnostics.Arrange("netbios header", "00 00 ff ff");

        SmbReceivedMessage received = await new SmbMessageReader(connection, TimeProvider.System).ReceiveAsync(CancellationToken.None);

        Diagnostics.Assert("error", "too large NetBIOS frame size 65539", received.ErrorMessage);
        Assert.AreEqual("too large NetBIOS frame size 65539", received.ErrorMessage);
        Assert.IsNull(received.Bytes);
    }

    [TestMethod]
    public async Task ReceiveAsync_FrameOfExactlyTheMaximum_IsAccepted()
    {
        byte[] message = new byte[SmbMessageReader.MaxMessageSize];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2), SmbMessageReader.MaxMessageSize - SmbMessageHeader.NetBiosHeaderLength);
        var connection = new ScriptedConnection(message);
        Diagnostics.Arrange("frame", SmbMessageReader.MaxMessageSize);

        SmbReceivedMessage received = await new SmbMessageReader(connection, TimeProvider.System).ReceiveAsync(CancellationToken.None);

        Diagnostics.Assert("received length", message.Length, received.Bytes?.Length);
        Assert.IsNull(received.ErrorMessage);
        Assert.HasCount(message.Length, received.Bytes!);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadOfExactlyTheMaximumPayload_AsksAgainAndEndsOnTheEmptyRead()
    {
        var output = new MemoryStream();
        var connection = Download(ReadReply(SmbReadRequest.MaxPayloadSize), ReadReply(0));

        TransferResult result = await RunAsync(connection, Context(output));

        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(SmbReadRequest.MaxPayloadSize, result.BytesTransferred);
        Assert.AreEqual(SmbReadRequest.MaxPayloadSize, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadOneShortOfTheMaximumPayload_EndsWithoutAskingAgain()
    {
        var output = new MemoryStream();
        var connection = Download(ReadReply(SmbReadRequest.MaxPayloadSize - 1));

        TransferResult result = await RunAsync(connection, Context(output));

        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(SmbReadRequest.MaxPayloadSize - 1, output.Length);
    }

    // ---- Malformed input ----

    [TestMethod]
    public void ReadResponse_NonZeroStatus_IsRefusedEvenWithValidData()
    {
        byte[] received = ReadReply(11);
        BinaryPrimitives.WriteUInt32LittleEndian(received.AsSpan(9), 0xc0000022);
        Diagnostics.Arrange("status", "0xc0000022");

        string? refusal = SmbReadResponse.TryGetData(received, out ReadOnlyMemory<byte> data);

        Diagnostics.Assert("refusal", "Failure when receiving data from the peer", refusal);
        Assert.AreEqual("Failure when receiving data from the peer", refusal);
        Assert.IsTrue(data.IsEmpty);
    }

    [TestMethod]
    public void ReadResponse_DataOffsetInsideTheHeader_NeverThrows()
    {
        byte[] received = ReadReply(11);
        BinaryPrimitives.WriteUInt16LittleEndian(received.AsSpan(ReadDataOffsetOffset), 0);
        Diagnostics.Arrange("data offset", 0);

        string? refusal = SmbReadResponse.TryGetData(received, out ReadOnlyMemory<byte> data);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(received[SmbMessageHeader.NetBiosHeaderLength..(SmbMessageHeader.NetBiosHeaderLength + 11)], data.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_ByteCountOnePastTheFrame_IsRefused()
    {
        byte[] message = ReadReply(11);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(ReadByteCountOffset), 12);
        var connection = new ScriptedConnection(message);
        Diagnostics.Arrange("byte count", 12);

        SmbReceivedMessage received = await new SmbMessageReader(connection, TimeProvider.System).ReceiveAsync(CancellationToken.None);

        Diagnostics.Assert("error", "Failure when receiving data from the peer", received.ErrorMessage);
        Assert.AreEqual("Failure when receiving data from the peer", received.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, received.ExitCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_WordCountPastTheFrame_IsAcceptedWithoutReadingPastIt()
    {
        byte[] message = new byte[SmbMessageHeader.Length + 2];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2), (ushort)(message.Length - SmbMessageHeader.NetBiosHeaderLength));
        message[SmbMessageHeader.Length] = 0xff;
        var connection = new ScriptedConnection(message);
        Diagnostics.Arrange("word count", 0xff);

        SmbReceivedMessage received = await new SmbMessageReader(connection, TimeProvider.System).ReceiveAsync(CancellationToken.None);

        Diagnostics.Assert("error", null, received.ErrorMessage);
        Assert.IsNull(received.ErrorMessage);
        CollectionAssert.AreEqual(message, received.Bytes);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeEndOfFile_Exits8()
    {
        byte[] open = SmbRecordedExchange.OpenAccepted;
        BinaryPrimitives.WriteInt64LittleEndian(open.AsSpan(92), -1);
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            open,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await RunAsync(connection, Context(new MemoryStream()));

        Diagnostics.AssertResult(CurlExitCode.WeirdServerReply, "Weird server reply", result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadDataPastTheMessage_Exits56InvalidInputPacket()
    {
        byte[] read = ReadReply(11);
        BinaryPrimitives.WriteUInt16LittleEndian(read.AsSpan(ReadDataOffsetOffset), 0x100);
        var output = new MemoryStream();

        TransferResult result = await RunAsync(Download(read), Context(output));

        Diagnostics.AssertResult(CurlExitCode.RecvError, "Invalid input packet", result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Invalid input packet", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadReplyEndingInsideTheDataOffsetWord_Exits56NotThrows()
    {
        byte[] read = ReadReply(11)[..(SmbMessageHeader.Length + 14)];
        BinaryPrimitives.WriteUInt16BigEndian(read.AsSpan(2), (ushort)(read.Length - SmbMessageHeader.NetBiosHeaderLength));
        var output = new MemoryStream();

        TransferResult result = await RunAsync(Download(read), Context(output));

        Diagnostics.AssertResult(CurlExitCode.RecvError, "Failure when receiving data from the peer", result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_TruncatedNegotiateResponse_Exits7()
    {
        byte[] negotiate = SmbRecordedExchange.NegotiateResponse[..80];
        BinaryPrimitives.WriteUInt16BigEndian(negotiate.AsSpan(2), (ushort)(negotiate.Length - SmbMessageHeader.NetBiosHeaderLength));
        negotiate[SmbMessageHeader.Length] = 0;
        var connection = new ScriptedConnection(negotiate);

        TransferResult result = await RunAsync(connection, Context(new MemoryStream()));

        Diagnostics.AssertResult(CurlExitCode.CouldntConnect, "Could not connect to server", result);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    // ---- Invalid partitions ----

    [TestMethod]
    [DataRow("/", "missing share in URL path for SMB")]
    [DataRow("/share", "missing share in URL path for SMB")]
    [DataRow(@"\share", "missing share in URL path for SMB")]
    [DataRow("/s/a%00b", "URL using bad/illegal format or missing URL")]
    [DataRow("/s/a%1fb", "URL using bad/illegal format or missing URL")]
    [DataRow("/s/a\tb", "URL using bad/illegal format or missing URL")]
    public void UrlPath_InvalidPartition_IsRefusedWithCurlsText(string absolutePath, string expected)
    {
        Diagnostics.Arrange("absolute path", absolutePath);

        string? refusal = SmbUrlPath.TryParse(absolutePath, out SmbUrlPath? path);

        Diagnostics.Assert("refusal", expected, refusal);
        Assert.AreEqual(expected, refusal);
        Assert.IsNull(path);
    }

    [TestMethod]
    [DataRow("/share/", "share", "")]
    [DataRow("/%2F/x", "", @"\x")]
    [DataRow("/s/../x", "s", @"..\x")]
    [DataRow(@"/s\a/b", @"s\a", "b")]
    [DataRow("/s/a%7Fb", "s", "a\u007fb")]
    [DataRow("/s/%", "s", "%")]
    [DataRow("/s/%4", "s", "%4")]
    [DataRow("/shé/日本.txt", "shé", "日本.txt")]
    [DataRow("/s/%C3%A9", "s", "é")]
    public void UrlPath_EdgePartition_SplitsAsCurlWithoutNormalising(string absolutePath, string share, string filePath)
    {
        Diagnostics.Arrange("absolute path", absolutePath);

        string? refusal = SmbUrlPath.TryParse(absolutePath, out SmbUrlPath? path);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.AreEqual(share, Encoding.UTF8.GetString(path!.Share));
        Assert.AreEqual(filePath, Encoding.UTF8.GetString(path.FilePath));
    }

    [TestMethod]
    public void UrlPath_InvalidUtf8Escape_KeepsTheRawBytes()
    {
        Diagnostics.Arrange("absolute path", "/s/%ff%fe");

        string? refusal = SmbUrlPath.TryParse("/s/%ff%fe", out SmbUrlPath? path);

        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(new byte[] { 0xff, 0xfe }, path!.FilePath);
    }

    [TestMethod]
    [DataRow("", "", "host")]
    [DataRow("/", "", "")]
    [DataRow(@"\", "", "")]
    [DataRow(@"a/b\c", @"b\c", "a")]
    [DataRow(@"a\b/c", "c", @"a\b")]
    [DataRow("a//b", "/b", "a")]
    public void Identity_SeparatorPartitions_SplitAsCurl(string userName, string user, string domain)
    {
        Diagnostics.Arrange("user name", userName);

        SmbIdentity identity = SmbIdentity.Split(userName, "host");

        Diagnostics.Assert("identity", $"{domain}|{user}", $"{identity.Domain}|{identity.User}");
        Assert.AreEqual(user, identity.User);
        Assert.AreEqual(domain, identity.Domain);
    }

    // ---- State and concurrency ----

    [TestMethod]
    public async Task ReceiveAsync_ThreeMessagesByteByByte_EachArrivesWhole()
    {
        byte[][] messages = [SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupAccepted, SmbRecordedExchange.TreeConnectAccepted];
        var connection = new ScriptedConnection([.. messages.SelectMany(message => message.Select(value => new[] { value }))]);
        var reader = new SmbMessageReader(connection, TimeProvider.System);
        Diagnostics.Arrange("reads", "one byte each, three messages");

        foreach (byte[] message in messages)
        {
            SmbReceivedMessage received = await reader.ReceiveAsync(CancellationToken.None);
            Assert.IsNull(received.ErrorMessage);
            CollectionAssert.AreEqual(message, received.Bytes);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerTwiceInARow_GivesTheSameResultBothTimes()
    {
        var connector = new QueueConnector(FullDownload(), FullDownload());
        var handler = new SmbProtocolHandler(connector, SmbCurlOperatingSystem.Linux);
        var first = new MemoryStream();
        var second = new MemoryStream();

        TransferResult firstResult = await handler.ExecuteAsync(Context(first));
        TransferResult secondResult = await handler.ExecuteAsync(Context(second));

        Diagnostics.AssertResult(CurlExitCode.Ok, null, secondResult);
        Assert.AreEqual(CurlExitCode.Ok, firstResult.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, secondResult.ExitCode);
        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
        Assert.AreEqual(SmbRecordedExchange.FileContent, Encoding.ASCII.GetString(second.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_EightConcurrentTransfersOnOneHandler_EachWritesItsOwnFile()
    {
        const int Transfers = 8;
        var connector = new QueueConnector([.. Enumerable.Range(0, Transfers).Select(_ => FullDownload())]);
        var handler = new SmbProtocolHandler(connector, SmbCurlOperatingSystem.Linux);
        MemoryStream[] outputs = [.. Enumerable.Range(0, Transfers).Select(_ => new MemoryStream())];
        Diagnostics.Arrange("transfers", Transfers);

        TransferResult[] results = await Task.WhenAll(outputs.Select(output => Task.Run(async () => await handler.ExecuteAsync(Context(output)))));

        Diagnostics.Assert("all ok", true, results.All(result => result.ExitCode == CurlExitCode.Ok));
        Assert.IsTrue(results.All(result => result.ExitCode == CurlExitCode.Ok));
        Assert.IsTrue(outputs.All(output => Encoding.ASCII.GetString(output.ToArray()) == SmbRecordedExchange.FileContent));
    }

    // A read response carrying dataLength bytes of 'a', laid out as curl's recorded reply.
    private static byte[] ReadReply(int dataLength)
    {
        byte[] reply = new byte[ReadDataStart + dataLength];
        SmbRecordedExchange.ReadAccepted.AsSpan(0, ReadDataStart).CopyTo(reply);
        reply.AsSpan(ReadDataStart).Fill((byte)'a');
        BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(2), (ushort)(reply.Length - SmbMessageHeader.NetBiosHeaderLength));
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(ReadDataLengthOffset), (ushort)dataLength);
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(ReadByteCountOffset), (ushort)dataLength);
        return reply;
    }

    private static ScriptedConnection Download(params byte[][] reads) => new(
        [
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            .. reads,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted,
        ]);

    private static ScriptedConnection FullDownload() => Download(SmbRecordedExchange.ReadAccepted);

    // A transfer that asks for more replies than scripted would wait on the closed
    // connection until cancelled; the token turns that hang into a failed test.
    private static TransferContext Context(Stream output) => new()
    {
        Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
        Output = output,
        Credentials = User,
        CancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token,
    };

    private async Task<TransferResult> RunAsync(ScriptedConnection connection, TransferContext context)
    {
        Diagnostics.ArrangeReplies(connection);
        TransferResult result = await new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux)
            .ExecuteAsync(context);
        Diagnostics.ActResult(result);
        return result;
    }

    // Hands each connect the next scripted connection, so one handler can run many transfers.
    private sealed class QueueConnector(params ScriptedConnection[] connections) : IConnector
    {
        private int next = -1;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connections[Interlocked.Increment(ref next)]));
    }
}
