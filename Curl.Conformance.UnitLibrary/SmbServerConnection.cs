using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to a <see cref="SmbServerConnector"/>, answering the SMBv1 requests curl
/// sends to download a file: SMB_COM_NEGOTIATE with a fixed challenge, SMB_COM_SESSION_SETUP_ANDX
/// with <see cref="UserId"/> (the NTLM responses are not checked), SMB_COM_TREE_CONNECT_ANDX to the
/// share <c>TESTS</c> with <see cref="TreeId"/>, SMB_COM_NT_CREATE_ANDX of any file with
/// <see cref="FileId"/> and the file's size, SMB_COM_READ_ANDX with the bytes at the offset asked
/// for, and SMB_COM_CLOSE and SMB_COM_TREE_DISCONNECT with success.
/// </summary>
/// <remarks>
/// Every whole message the client writes, framed by its NetBIOS session header, is answered at once,
/// however the writes split it; a read with nothing waiting waits for the client's next write. A tree
/// connect to another share is refused with <c>STATUS_BAD_NETWORK_NAME</c>, and any other command,
/// such as an upload's SMB_COM_WRITE_ANDX, with <c>STATUS_NOT_IMPLEMENTED</c>.
/// </remarks>
internal sealed class SmbServerConnection : IConnection
{
    /// <summary>The UID the session setup response assigns.</summary>
    public const ushort UserId = 100;

    /// <summary>The TID the tree connect response assigns.</summary>
    public const ushort TreeId = 200;

    /// <summary>The FID the open response assigns.</summary>
    public const ushort FileId = 300;

    /// <summary>STATUS_BAD_NETWORK_NAME, a tree connect to a share other than <c>TESTS</c>.</summary>
    public const uint BadNetworkName = 0xC00000CC;

    /// <summary>STATUS_NOT_IMPLEMENTED, a command the stand-in does not serve.</summary>
    public const uint NotImplemented = 0xC0000002;

    private const string Share = "TESTS";

    private const int NetBiosHeaderLength = 4;

    private const int HeaderLength = 36;

    private const int StatusOffset = 9;

    private const int FlagsOffset = 13;

    private const int TreeIdOffset = 28;

    private const int UserIdOffset = 32;

    // SMB_FLAGS_REPLY | SMB_FLAGS_CANONICAL_PATHNAMES | SMB_FLAGS_CASELESS_PATHNAMES.
    private const byte ReplyFlags = 0x98;

    private const byte NegotiateCommand = 0x72;
    private const byte SessionSetupAndXCommand = 0x73;
    private const byte TreeConnectAndXCommand = 0x75;
    private const byte NtCreateAndXCommand = 0xa2;
    private const byte ReadAndXCommand = 0x2e;
    private const byte CloseCommand = 0x04;
    private const byte TreeDisconnectCommand = 0x71;

    // Where a tree connect's bytes start: its word count, four words and byte count.
    private const int TreeConnectBytesOffset = HeaderLength + 11;

    // Where a read's offset, byte count and high offset sit.
    private const int ReadOffsetOffset = HeaderLength + 7;
    private const int ReadMaxCountOffset = HeaderLength + 11;
    private const int ReadOffsetHighOffset = HeaderLength + 21;

    private static readonly byte[] Magic = [0xff, (byte)'S', (byte)'M', (byte)'B'];

    private static readonly byte[] Challenge = [0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef];

    private readonly byte[] fileContent;

    private readonly Lock gate = new();

    private readonly List<byte> unreadReplyBytes = [];

    private readonly List<byte> unreadClientBytes = [];

    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Dictionary<byte, Func<byte[], byte[]>> answerers;

    public SmbServerConnection(byte[] fileContent)
    {
        this.fileContent = fileContent;
        answerers = Answerers();
    }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public EndPoint? LocalEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task written;
        lock (gate)
        {
            if (unreadReplyBytes.Count > 0)
            {
                int count = Math.Min(buffer.Length, unreadReplyBytes.Count);
                CollectionsMarshal.AsSpan(unreadReplyBytes)[..count].CopyTo(buffer.Span);
                unreadReplyBytes.RemoveRange(0, count);
                return count;
            }

            written = nextWrite.Task;
        }

        await written.WaitAsync(cancellationToken);
        return await ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        TaskCompletionSource written;
        lock (gate)
        {
            unreadClientBytes.AddRange(buffer.Span);
            while (AnswerNextMessage())
            {
            }

            written = nextWrite;
            nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        written.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Answers the first whole message waiting; false when none is whole yet.
    private bool AnswerNextMessage()
    {
        if (unreadClientBytes.Count < NetBiosHeaderLength)
        {
            return false;
        }

        int length = NetBiosHeaderLength + BinaryPrimitives.ReadUInt16BigEndian(CollectionsMarshal.AsSpan(unreadClientBytes)[2..]);
        if (unreadClientBytes.Count < length)
        {
            return false;
        }

        byte[] message = unreadClientBytes.GetRange(0, length).ToArray();
        unreadClientBytes.RemoveRange(0, length);
        unreadReplyBytes.AddRange(Answer(message));
        return true;
    }

    private byte[] Answer(byte[] request) =>
        request.Length >= HeaderLength && answerers.TryGetValue(request[8], out Func<byte[], byte[]>? answer)
            ? answer(request)
            : Reply(request, NotImplemented, [0, 0, 0]);

    private Dictionary<byte, Func<byte[], byte[]>> Answerers() => new()
    {
        [NegotiateCommand] = request => Reply(request, 0, NegotiateBody()),
        [SessionSetupAndXCommand] = request => Reply(request, 0, [3, .. new byte[6], 0, 0], userId: UserId),
        [TreeConnectAndXCommand] = AnswerTreeConnect,
        [NtCreateAndXCommand] = request => Reply(request, 0, OpenBody()),
        [ReadAndXCommand] = request => Reply(request, 0, ReadBody(request)),
        [CloseCommand] = request => Reply(request, 0, [0, 0, 0]),
        [TreeDisconnectCommand] = request => Reply(request, 0, [0, 0, 0]),
    };

    // Seventeen words, then the challenge as the eight bytes: the challenge lands at byte 73 and
    // the encryption key length word says 8, as curl reads them.
    private static byte[] NegotiateBody()
    {
        byte[] body = new byte[1 + 34 + 2 + Challenge.Length];
        body[0] = 17;
        body[1 + 33] = (byte)Challenge.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1 + 34), (ushort)Challenge.Length);
        Challenge.CopyTo(body, 1 + 34 + 2);
        return body;
    }

    // The share is what follows the last backslash of \\host\share, up to its NUL.
    private static byte[] AnswerTreeConnect(byte[] request)
    {
        string path = Encoding.Latin1.GetString(request.AsSpan(Math.Min(TreeConnectBytesOffset, request.Length)));
        string hostAndShare = path.Split('\0')[0];
        string share = hostAndShare[(hostAndShare.LastIndexOf('\\') + 1)..];
        return string.Equals(share, Share, StringComparison.OrdinalIgnoreCase)
            ? Reply(request, 0, [0, 0, 0], userId: UserId, treeId: TreeId)
            : Reply(request, BadNetworkName, [0, 0, 0]);
    }

    // Thirty-four words: the FID at byte 42 and the end of file at byte 92 of the message, the
    // same for whichever file is named.
    private byte[] OpenBody()
    {
        byte[] body = new byte[1 + 68 + 2];
        body[0] = 34;
        body[1] = 0xff;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(42 - HeaderLength), FileId);
        BinaryPrimitives.WriteInt64LittleEndian(body.AsSpan(92 - HeaderLength), fileContent.Length);
        return body;
    }

    // Twelve words, the data length at byte 47 and its offset from the SMB header at byte 49,
    // then the bytes at the offset asked for, at most as many as asked for.
    private byte[] ReadBody(byte[] request)
    {
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(ReadOffsetOffset))
            | ((long)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(ReadOffsetHighOffset)) << 32);
        int maxCount = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(ReadMaxCountOffset));
        int start = (int)Math.Min(offset, fileContent.Length);
        int count = Math.Min(maxCount, fileContent.Length - start);
        byte[] body = new byte[1 + 24 + 2 + count];
        body[0] = 12;
        body[1] = 0xff;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(47 - HeaderLength), (ushort)count);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(49 - HeaderLength), (ushort)(HeaderLength + 1 + 24 + 2 - NetBiosHeaderLength));
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1 + 24), (ushort)count);
        fileContent.AsSpan(start, count).CopyTo(body.AsSpan(1 + 24 + 2));
        return body;
    }

    // The request's header with the reply flag, the status and the IDs, then the body.
    private static byte[] Reply(byte[] request, uint status, byte[] body, ushort? userId = null, ushort? treeId = null)
    {
        byte[] reply = new byte[HeaderLength + body.Length];
        request.AsSpan(0, Math.Min(HeaderLength, request.Length)).CopyTo(reply);
        Magic.CopyTo(reply, NetBiosHeaderLength);
        reply[0] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(2), (ushort)(reply.Length - NetBiosHeaderLength));
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(StatusOffset), status);
        reply[FlagsOffset] = ReplyFlags;
        if (userId is { } uid)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(UserIdOffset), uid);
        }

        if (treeId is { } tid)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(TreeIdOffset), tid);
        }

        body.CopyTo(reply, HeaderLength);
        return reply;
    }
}
