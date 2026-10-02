using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Curl.Networking;

/// <summary>
/// Connects a TCP socket on macOS through <c>connectx</c> with <c>CONNECT_DATA_IDEMPOTENT |
/// CONNECT_RESUME_ON_READ_WRITE</c>, the route libcurl 8.21.0's <c>cf-socket.c</c> takes for
/// <c>--tcp-fastopen</c> on Darwin, so the SYN goes out with the first bytes written (BL-1101, ADR-0355).
/// The BCL does not wrap <c>connectx</c>, so it is called through <c>libc</c>.
/// </summary>
/// <remarks>
/// Excluded from coverage per ADR-0083, the generated marshalling with it: a thin adapter over two system
/// calls that only macOS has, pinned by the macOS test in <c>DarwinFastOpenConnectTests</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin adapter over macOS system calls, measured by the macOS test.")]
internal static partial class DarwinFastOpenConnect
{
    // <sys/socket.h>: CONNECT_RESUME_ON_READ_WRITE, CONNECT_DATA_IDEMPOTENT and SAE_ASSOCID_ANY.
    private const uint ConnectResumeOnReadWrite = 0x1;
    private const uint ConnectDataIdempotent = 0x2;
    private const uint SaeAssociationIdAny = 0;

    /// <summary>
    /// Connects <paramref name="socket" /> to <paramref name="endPoint" /> through <c>connectx</c> and answers
    /// a <see cref="Socket" /> that knows it is connected, over a duplicate of the same descriptor, disposing
    /// <paramref name="socket" />; or <see langword="null" />, leaving <paramref name="socket" /> as it was,
    /// when not on macOS or when <c>connectx</c> refuses, so the caller connects as usual.
    /// </summary>
    /// <remarks>
    /// The connect is deferred until the first write, which carries the SYN. A managed <see cref="Socket" />
    /// learns it is connected only from its own connect or from the peer name it reads when made from a
    /// handle, hence the duplicate descriptor.
    /// </remarks>
    /// <param name="socket">A TCP socket with its options set, not yet connected.</param>
    /// <param name="endPoint">The server's end point.</param>
    /// <returns>The connected socket, or <see langword="null" />.</returns>
    internal static unsafe Socket? TryConnect(Socket socket, IPEndPoint endPoint)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var descriptor = (int)socket.SafeHandle.DangerousGetHandle();
        var address = endPoint.Serialize();
        int result;
        fixed (byte* destination = address.Buffer.Span)
        {
            var endpoints = new SocketAddressEndpoints
            {
                DestinationAddress = (nint)destination,
                DestinationAddressLength = (uint)address.Size,
            };
            result = Connectx(descriptor, &endpoints, SaeAssociationIdAny, ConnectResumeOnReadWrite | ConnectDataIdempotent, 0, 0, 0, 0);
        }

        if (result != 0)
        {
            return null;
        }

        var duplicate = Duplicate(descriptor);
        if (duplicate < 0)
        {
            // dup fails only when the process is out of descriptors (EMFILE).
            throw new SocketException((int)SocketError.TooManyOpenSockets);
        }

        var connected = new Socket(new SafeSocketHandle(duplicate, ownsHandle: true));
        socket.Dispose();
        return connected;
    }

    [LibraryImport("libc", EntryPoint = "connectx", SetLastError = true)]
    private static unsafe partial int Connectx(int socket, SocketAddressEndpoints* endpoints, uint associationId, uint flags, nint ioVectors, uint ioVectorCount, nint length, nint connectionId);

    [LibraryImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static partial int Duplicate(int descriptor);

    // <sys/socket.h>'s sa_endpoints_t: no source interface or address, only the destination.
    [StructLayout(LayoutKind.Sequential)]
    private struct SocketAddressEndpoints
    {
        public uint SourceInterface;
        public nint SourceAddress;
        public uint SourceAddressLength;
        public nint DestinationAddress;
        public uint DestinationAddressLength;
    }
}
