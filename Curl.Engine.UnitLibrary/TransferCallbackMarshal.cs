using Curl.Api.Callbacks;
using Curl.Api.Codes;

namespace Curl.Engine;

/// <summary>
/// Puts every callback of one multi-driven perform through its multi handle's <see cref="MultiCallbackQueue" />,
/// so each runs on the thread calling <c>curl_multi_perform</c>, inside the call (CGR-033). Once the perform has
/// ended (<see cref="Stop" />) the callbacks run where they are called, as a connection the pool closes later
/// calls its close-socket callback from wherever it is closed.
/// </summary>
/// <param name="queue">The multi handle's queue.</param>
/// <param name="cancellationToken">The transfer's token in the multi handle, which ends any wait for a perform.</param>
internal sealed class TransferCallbackMarshal(MultiCallbackQueue queue, CancellationToken cancellationToken)
{
    private volatile bool running = true;

    /// <summary>Lets every callback run where it is called from now on.</summary>
    public void Stop() => running = false;

    /// <summary>The callbacks of <paramref name="callbacks" />, each one set running through the queue.</summary>
    /// <param name="callbacks">The perform's callbacks.</param>
    /// <returns>The marshalled callbacks.</returns>
    public EasyCallbackOptions Over(EasyCallbackOptions callbacks) => callbacks with
    {
        Write = Marshalled(callbacks.Write, write => data => Run(data.ToArray(), copy => write(copy))),
        Read = Marshalled(callbacks.Read, read => buffer => ReadThrough(read, buffer)),
        Header = Marshalled(callbacks.Header, header => data => Run(data.ToArray(), copy => header(copy))),
        Progress = Marshalled(callbacks.Progress, progress => (dt, dn, ut, un) => Run(() => progress(dt, dn, ut, un))),
        TransferInfo = Marshalled(callbacks.TransferInfo, info => (dt, dn, ut, un) => Run(() => info(dt, dn, ut, un))),
        Debug = Marshalled(callbacks.Debug, debug => (type, data) => DebugThrough(debug, type, data.ToArray())),
        Seek = Marshalled(callbacks.Seek, seek => (offset, origin) => Run(() => seek(offset, origin))),
        Ioctl = Marshalled(callbacks.Ioctl, ioctl => command => Run(() => ioctl(command))),
        SocketOption = Marshalled(callbacks.SocketOption, option => (socket, purpose) => Run(() => option(socket, purpose))),
        OpenSocket = Marshalled(callbacks.OpenSocket, open => (purpose, address) => Run(() => open(purpose, address))),
        CloseSocket = Marshalled(callbacks.CloseSocket, close => socket => Run(() => close(socket))),
        Prerequest = Marshalled(callbacks.Prerequest, prerequest => (peer, local, peerPort, localPort) => Run(() => prerequest(peer, local, peerPort, localPort))),
        ResolverStart = Marshalled(callbacks.ResolverStart, resolverStart => state => Run(() => resolverStart(state))),
        Trailer = Marshalled(callbacks.Trailer, trailer => lines => Run(() => trailer(lines))),
        ChunkBegin = Marshalled(callbacks.ChunkBegin, chunkBegin => (file, remains) => Run(() => chunkBegin(file, remains))),
        ChunkEnd = Marshalled(callbacks.ChunkEnd, chunkEnd => () => Run(() => chunkEnd())),
        FileNameMatch = Marshalled(callbacks.FileNameMatch, match => (pattern, name) => Run(() => match(pattern, name))),
        Interleave = Marshalled(callbacks.Interleave, interleave => data => Run(data.ToArray(), copy => interleave(copy))),
        HstsRead = Marshalled(callbacks.HstsRead, hstsRead => (out HstsEntry? entry) => HstsReadThrough(hstsRead, out entry)),
        HstsWrite = Marshalled(callbacks.HstsWrite, hstsWrite => (entry, index, total) => Run(() => hstsWrite(entry, index, total))),
        SshKey = Marshalled(callbacks.SshKey, sshKey => (known, found, keyMatch) => Run(() => sshKey(known, found, keyMatch))),
        SshHostKey = Marshalled(callbacks.SshHostKey, hostKey => (type, key) => SshHostKeyThrough(hostKey, type, key.ToArray())),
        SslContext = Marshalled(callbacks.SslContext, sslContext => context => Run(() => sslContext(context))),
    };

    private T Run<T>(Func<T> callback) => running ? queue.Run(callback, cancellationToken) : callback();

    private static T? Marshalled<T>(T? callback, Func<T, T> marshal)
        where T : Delegate => callback is null ? null : marshal(callback);

    private long Run(byte[] data, Func<byte[], long> callback) => Run(() => callback(data));

    private int DebugThrough(DebugCallback debug, CurlInfoType type, byte[] data) => Run(() => debug(type, data));

    private SshKeyMatch SshHostKeyThrough(SshHostKeyCallback callback, SshKeyType type, byte[] key) => Run(() => callback(type, key));

    // The callback fills a buffer of the performing thread's, copied into the transfer's afterwards.
    private long ReadThrough(ReadCallback read, Span<byte> buffer)
    {
        byte[] filled = new byte[buffer.Length];
        long count = Run(() => read(filled));
        if (count > 0 && count <= filled.Length)
        {
            filled.AsSpan(0, (int)count).CopyTo(buffer);
        }

        return count;
    }

    private HstsResult HstsReadThrough(HstsReadCallback read, out HstsEntry? entry)
    {
        (HstsResult result, HstsEntry? read) answer = Run(() => (read(out HstsEntry? given), given));
        entry = answer.read;
        return answer.result;
    }
}
