using Curl.Api.Callbacks;
using Curl.Api.Codes;

namespace Curl.Engine;

[TestClass]
public sealed class TransferCallbackMarshalTests
{
    [TestMethod]
    public void Over_NoCallbacks_SetsNone()
    {
        TransferCallbackMarshal marshal = new(new MultiCallbackQueue(), CancellationToken.None);

        EasyCallbackOptions marshalled = marshal.Over(new EasyCallbackOptions());

        Assert.AreEqual(new EasyCallbackOptions(), marshalled);
    }

    [TestMethod]
    public void Over_EveryCallbackOnThePerformingThread_PassesArgumentsAndAnswersThrough()
    {
        MultiCallbackQueue queue = new();
        TransferCallbackMarshal marshal = new(queue, CancellationToken.None);
        List<string> calls = [];
        HstsEntry hstsEntry = new("example.com", IncludeSubDomains: false, Expire: null);
        EasyCallbackOptions callbacks = new()
        {
            Write = data => Record(calls, "write", data.Length),
            Read = buffer =>
            {
                buffer[0] = 0x41;
                return Record(calls, "read", 1);
            },
            Header = data => Record(calls, "header", data.Length),
            Progress = (dt, dn, ut, un) => (int)Record(calls, "progress", (long)(dt + dn + ut + un)),
            TransferInfo = (dt, dn, ut, un) => (int)Record(calls, "xferinfo", dt + dn + ut + un),
            Debug = (type, data) => (int)Record(calls, $"debug {type}", data.Length),
            Seek = (offset, origin) => Record(calls, $"seek {origin}", offset) == 0 ? SeekResult.Ok : SeekResult.Fail,
            Ioctl = command => Record(calls, $"ioctl {command}", 0) == 0 ? IoctlResult.Ok : IoctlResult.UnknownCommand,
            SocketOption = (socket, purpose) => Record(calls, $"sockopt {purpose}", socket.Handle) == 5 ? SocketOptionResult.AlreadyConnected : SocketOptionResult.Ok,
            OpenSocket = (purpose, address) => new CurlSocket((nint)Record(calls, $"opensocket {purpose}", 9)),
            CloseSocket = socket => (int)Record(calls, "closesocket", socket.Handle),
            Prerequest = (peer, local, peerPort, localPort) => Record(calls, $"prereq {peer} {local}", peerPort + localPort) == 3 ? PrerequestResult.Abort : PrerequestResult.Ok,
            ResolverStart = state => (int)Record(calls, "resolver", state),
            Trailer = lines =>
            {
                lines.Add("X: y");
                return Record(calls, "trailer", 0) == 0 ? TrailerResult.Ok : TrailerResult.Abort;
            },
            ChunkBegin = (file, remains) => Record(calls, "chunkbegin", remains) == 2 ? ChunkBeginResult.Skip : ChunkBeginResult.Ok,
            ChunkEnd = () => Record(calls, "chunkend", 0) == 0 ? ChunkEndResult.Ok : ChunkEndResult.Fail,
            FileNameMatch = (pattern, name) => Record(calls, $"fnmatch {pattern} {name}", 0) == 0 ? FileNameMatchResult.Match : FileNameMatchResult.NoMatch,
            Interleave = data => Record(calls, "interleave", data.Length),
            HstsRead = (out HstsEntry? entry) =>
            {
                entry = hstsEntry;
                return Record(calls, "hstsread", 0) == 0 ? HstsResult.Ok : HstsResult.Fail;
            },
            HstsWrite = (entry, index, total) => Record(calls, $"hstswrite {entry.Name}", index + total) == 3 ? HstsResult.Done : HstsResult.Ok,
            SshKey = (known, found, match) => Record(calls, $"sshkey {match}", 0) == 0 ? SshKeyStatus.FineAddToFile : SshKeyStatus.Reject,
            SshHostKey = (type, key) => Record(calls, $"hostkey {type}", key.Length) == 4 ? SshKeyMatch.Ok : SshKeyMatch.Mismatch,
            SslContext = context => Record(calls, "sslctx", context) == 0 ? CurlCode.Ok : CurlCode.SslCertProblem,
        };
        int previous = queue.EnterPerform();

        EasyCallbackOptions marshalled = marshal.Over(callbacks);
        byte[] readBuffer = new byte[4];
        List<string> trailers = [];
        long[] answers =
        [
            marshalled.Write!([1, 2, 3]),
            marshalled.Read!(readBuffer),
            marshalled.Header!([1, 2]),
            marshalled.Progress!(1, 2, 3, 4),
            marshalled.TransferInfo!(1, 2, 3, 4),
            marshalled.Debug!(CurlInfoType.Text, [1]),
            (long)marshalled.Seek!(0, SeekOrigin.Begin),
            (long)marshalled.Ioctl!(IoctlCommand.RestartRead),
            (long)marshalled.SocketOption!(new CurlSocket(5), CurlSocketType.IpConnection),
            marshalled.OpenSocket!(CurlSocketType.IpConnection, default!).Handle,
            marshalled.CloseSocket!(new CurlSocket(6)),
            (long)marshalled.Prerequest!("1.2.3.4", "5.6.7.8", 1, 2),
            marshalled.ResolverStart!(8),
            (long)marshalled.Trailer!(trailers),
            (long)marshalled.ChunkBegin!(default!, 2),
            (long)marshalled.ChunkEnd!(),
            (long)marshalled.FileNameMatch!("*", "a"),
            marshalled.Interleave!([1, 2, 3, 4, 5]),
            (long)marshalled.HstsRead!(out HstsEntry? readEntry),
            (long)marshalled.HstsWrite!(hstsEntry, 1, 2),
            (long)marshalled.SshKey!(null, default!, SshKeyMatch.Missing),
            (long)marshalled.SshHostKey!(SshKeyType.Ed25519, [1, 2, 3, 4]),
            (long)marshalled.SslContext!(0),
        ];
        queue.LeavePerform(previous);

        CollectionAssert.AreEqual(
            new long[]
            {
                3, 1, 2, 10, 10, 1, (long)SeekResult.Ok, (long)IoctlResult.Ok, (long)SocketOptionResult.AlreadyConnected, 9, 6,
                (long)PrerequestResult.Abort, 8, (long)TrailerResult.Ok, (long)ChunkBeginResult.Skip, (long)ChunkEndResult.Ok,
                (long)FileNameMatchResult.Match, 5, (long)HstsResult.Ok, (long)HstsResult.Done, (long)SshKeyStatus.FineAddToFile,
                (long)SshKeyMatch.Ok, (long)CurlCode.Ok,
            },
            answers);
        Assert.AreEqual(23, calls.Count);
        Assert.AreEqual(0x41, readBuffer[0]);
        CollectionAssert.AreEqual(new[] { "X: y" }, trailers);
        Assert.AreSame(hstsEntry, readEntry);
    }

    [TestMethod]
    public async Task Over_WriteFromAnotherThread_RunsOnTheThreadRunningTheQueue()
    {
        MultiCallbackQueue queue = new();
        TransferCallbackMarshal marshal = new(queue, CancellationToken.None);
        int callbackThread = 0;
        WriteCallback write = marshal.Over(new EasyCallbackOptions
        {
            Write = data =>
            {
                callbackThread = Environment.CurrentManagedThreadId;
                return data.Length;
            },
        }).Write!;

        Task<long> transfer = Task.Run(() => write([1, 2]));
        while (!transfer.IsCompleted)
        {
            queue.RunQueued();
        }

        Assert.AreEqual(2, await transfer);
        Assert.AreEqual(Environment.CurrentManagedThreadId, callbackThread);
    }

    [TestMethod]
    public void Over_ReadAnswersNothingOrMoreThanTheBuffer_CopiesNothingAndPassesTheAnswer()
    {
        MultiCallbackQueue queue = new();
        TransferCallbackMarshal marshal = new(queue, CancellationToken.None);
        marshal.Stop();
        ReadCallback read = marshal.Over(new EasyCallbackOptions
        {
            Read = buffer =>
            {
                buffer[0] = 0x41;
                return buffer.Length == 2 ? ReadCallbackResult.Pause : 0;
            },
        }).Read!;
        byte[] buffer = new byte[2];

        long answer = read(buffer);
        long nothing = read(new byte[3]);

        Assert.AreEqual(ReadCallbackResult.Pause, answer);
        Assert.AreEqual(0, nothing);
        Assert.AreEqual(0, buffer[0]);
    }

    private static long Record(List<string> calls, string call, long answer)
    {
        calls.Add(call);
        return answer;
    }
}
