using System.Buffers.Binary;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One connection to <see cref="SocksServerConnector"/>'s socksd emulation: it answers the
/// client's SOCKS handshake from what the client writes, then relays to a connection to the
/// backend server.
/// </summary>
internal sealed class SocksServerConnection(SocksServerConfiguration configuration, IConnector backend) : IConnection
{
    private readonly List<byte> pendingRequest = [];

    private byte[] pendingReply = [];

    private IConnection? relay;

    private bool greeted;

    private bool authenticated;

    private bool closed;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pendingReply.Length > 0)
        {
            int count = Math.Min(buffer.Length, pendingReply.Length);
            pendingReply.AsSpan(0, count).CopyTo(buffer.Span);
            pendingReply = pendingReply[count..];
            return count;
        }

        return relay is null ? 0 : await relay.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (relay is not null)
        {
            await relay.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (closed)
        {
            return;
        }

        pendingRequest.AddRange(buffer.Span);
        int consumed;
        while (relay is null && !closed && pendingRequest.Count > 0 && (consumed = await AnswerAsync([.. pendingRequest], cancellationToken).ConfigureAwait(false)) > 0)
        {
            pendingRequest.RemoveRange(0, consumed);
        }
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        relay is null ? ValueTask.CompletedTask : relay.FlushAsync(cancellationToken);

    public ValueTask DisposeAsync() => relay is null ? ValueTask.CompletedTask : relay.DisposeAsync();

    // Answers the next complete message at the start of the request bytes, returning how many
    // bytes it took, or 0 when the message is not complete yet.
    private ValueTask<int> AnswerAsync(byte[] request, CancellationToken cancellationToken) =>
        request[0] switch
        {
            4 => AnswerSocks4Async(request, cancellationToken),
            5 => AnswerSocks5Async(request, cancellationToken),
            1 when greeted && !authenticated => ValueTask.FromResult(AnswerCredentials(request)),
            _ => ValueTask.FromResult(Close()),
        };

    private ValueTask<int> AnswerSocks5Async(byte[] request, CancellationToken cancellationToken) =>
        greeted ? AnswerSocks5RequestAsync(request, cancellationToken) : ValueTask.FromResult(AnswerGreeting(request));

    // VN CD DSTPORT(2) DSTIP(4) USERID NUL, then for SOCKS4a (DSTIP 0.0.0.x, x not 0) HOST NUL.
    private async ValueTask<int> AnswerSocks4Async(byte[] request, CancellationToken cancellationToken)
    {
        (int userEnd, bool namesHost, int end) = FindSocks4End(request);
        if (end < 0)
        {
            return 0;
        }

        string host = namesHost
            ? Encoding.Latin1.GetString(request, userEnd + 1, end - userEnd - 1)
            : new IPAddress(request.AsSpan(4, 4)).ToString();
        bool connected = await ConnectBackendAsync(host, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(2)), cancellationToken).ConfigureAwait(false);
        pendingReply = [.. pendingReply, 0, (byte)(connected ? 90 : 91), .. request.AsSpan(2, 6)];
        return end + 1;
    }

    // Where the USERID ends, whether DSTIP names a host to follow, and where the message ends (-1 when incomplete).
    private static (int UserEnd, bool NamesHost, int End) FindSocks4End(byte[] request)
    {
        int userEnd = request.Length < 8 ? -1 : Array.IndexOf(request, (byte)0, 8);
        bool namesHost = userEnd >= 0 && BinaryPrimitives.ReadUInt32BigEndian(request.AsSpan(4)) - 1 < 255;
        int end = namesHost ? Array.IndexOf(request, (byte)0, userEnd + 1) : userEnd;
        return (userEnd, namesHost, end);
    }

    // VER NMETHODS METHODS, answered with VER and the configured method.
    private int AnswerGreeting(byte[] request)
    {
        if (request.Length < 2 || request.Length < 2 + request[1])
        {
            return 0;
        }

        greeted = true;
        authenticated = configuration.Method != 2;
        pendingReply = [.. pendingReply, 5, configuration.Method];
        return 2 + request[1];
    }

    // VER ULEN UNAME PLEN PASSWD, answered with VER and 0 for the configured user and password, else 1 and a close.
    private int AnswerCredentials(byte[] request)
    {
        int passwordLengthAt = request.Length < 2 ? int.MaxValue : 2 + request[1];
        if (passwordLengthAt >= request.Length || request.Length < passwordLengthAt + 1 + request[passwordLengthAt])
        {
            return 0;
        }

        string user = Encoding.Latin1.GetString(request, 2, request[1]);
        string password = Encoding.Latin1.GetString(request, passwordLengthAt + 1, request[passwordLengthAt]);
        authenticated = user == configuration.User && password == configuration.Password;
        closed = !authenticated;
        pendingReply = [.. pendingReply, 1, (byte)(authenticated ? 0 : 1)];
        return passwordLengthAt + 1 + request[passwordLengthAt];
    }

    // VER CMD RSV ATYP DST.ADDR DST.PORT(2), answered with the request's bytes and REP 0.
    private async ValueTask<int> AnswerSocks5RequestAsync(byte[] request, CancellationToken cancellationToken)
    {
        int end = authenticated ? Socks5RequestEnd(request) : -1;
        if (end < 0)
        {
            return Close();
        }

        if (end == 0)
        {
            return 0;
        }

        int addressLength = end - 6;
        string host = request[3] == 3
            ? Encoding.Latin1.GetString(request, 5, request[4])
            : new IPAddress(request.AsSpan(4, addressLength)).ToString();
        bool connected = await ConnectBackendAsync(host, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(end - 2)), cancellationToken).ConfigureAwait(false);
        pendingReply = [.. pendingReply, 5, (byte)(connected ? 0 : 5), .. request.AsSpan(2, end - 2)];
        return end;
    }

    // Where the request ends: 0 while it is incomplete, -1 for a command or address type that is not supported.
    private static int Socks5RequestEnd(byte[] request)
    {
        if (request.Length < 5)
        {
            return 0;
        }

        int addressLength = Socks5AddressLength(request);
        if (addressLength == -2 || request[1] != 1)
        {
            return -1;
        }

        int end = 4 + addressLength + 2;
        return request.Length < end ? 0 : end;
    }

    // ATYP's address length: 4 for IPv4, 16 for IPv6, a length byte and the name for a host, -2 for any other type.
    private static int Socks5AddressLength(byte[] request) =>
        request[3] switch
        {
            1 => 4,
            4 => 16,
            3 => 1 + request[4],
            _ => -2,
        };

    // A backend that refuses the connection (%NOLISTENPORT) closes this one after the failure
    // reply, as socksd answers a failed connect and hangs up.
    private async ValueTask<bool> ConnectBackendAsync(string host, int port, CancellationToken cancellationToken)
    {
        int backendPort = configuration.BackendPort == 0 ? port : configuration.BackendPort;
        ConnectResult result = await backend.ConnectAsync(new ConnectTarget(host, backendPort, false), cancellationToken).ConfigureAwait(false);
        relay = result.Connection;
        closed = relay is null;
        return !closed;
    }

    private int Close()
    {
        closed = true;
        return 0;
    }
}
