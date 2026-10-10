using System.Buffers.Binary;
using System.Globalization;
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
            5 when !greeted => ValueTask.FromResult(AnswerGreeting(request)),
            5 => AnswerSocks5RequestAsync(request, cancellationToken),
            1 when greeted && !authenticated => ValueTask.FromResult(AnswerCredentials(request)),
            _ => ValueTask.FromResult(Close()),
        };

    // VN CD DSTPORT(2) DSTIP(4) USERID NUL, then for SOCKS4a (DSTIP 0.0.0.x, x not 0) HOST NUL.
    private async ValueTask<int> AnswerSocks4Async(byte[] request, CancellationToken cancellationToken)
    {
        int userEnd = request.Length < 8 ? -1 : Array.IndexOf(request, (byte)0, 8);
        bool namesHost = userEnd >= 0 && BinaryPrimitives.ReadUInt32BigEndian(request.AsSpan(4)) - 1 < 255;
        int end = namesHost ? Array.IndexOf(request, (byte)0, userEnd + 1) : userEnd;
        if (end < 0)
        {
            return 0;
        }

        string host = namesHost
            ? Encoding.Latin1.GetString(request, userEnd + 1, end - userEnd - 1)
            : new IPAddress(request.AsSpan(4, 4)).ToString();
        await ConnectBackendAsync(host, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(2)), cancellationToken).ConfigureAwait(false);
        pendingReply = [.. pendingReply, 0, 90, .. request.AsSpan(2, 6)];
        return end + 1;
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
        if (!authenticated)
        {
            return Close();
        }

        if (request.Length < 5)
        {
            return 0;
        }

        int addressLength = request[3] switch
        {
            1 => 4,
            4 => 16,
            3 => 1 + request[4],
            _ => -2,
        };
        if (addressLength == -2 || request[1] != 1)
        {
            return Close();
        }

        int end = 4 + addressLength + 2;
        if (request.Length < end)
        {
            return 0;
        }

        string host = request[3] == 3
            ? Encoding.Latin1.GetString(request, 5, request[4])
            : new IPAddress(request.AsSpan(4, addressLength)).ToString();
        await ConnectBackendAsync(host, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(end - 2)), cancellationToken).ConfigureAwait(false);
        pendingReply = [.. pendingReply, 5, 0, .. request.AsSpan(2, end - 2)];
        return end;
    }

    private async ValueTask ConnectBackendAsync(string host, int port, CancellationToken cancellationToken)
    {
        int backendPort = configuration.BackendPort == 0 ? port : configuration.BackendPort;
        ConnectResult result = await backend.ConnectAsync(new ConnectTarget(host, backendPort, false), cancellationToken).ConfigureAwait(false);
        relay = result.Connection ?? throw new IOException(string.Create(CultureInfo.InvariantCulture, $"The SOCKS backend refused {host}:{backendPort}."));
    }

    private int Close()
    {
        closed = true;
        return 0;
    }
}
