using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Sftp;
using Curl.Protocol.Ssh.Transport;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// One connection's session on an <see cref="InMemorySshServer" />, the server's side of the
/// whole exchange: identification, <c>KEXINIT</c>, <c>diffie-hellman-group14-sha256</c> signed
/// with the RSA host key, <c>NEWKEYS</c>, then every message until the client disconnects or
/// closes.
/// </summary>
/// <param name="server">The server whose files, user and switches the session serves.</param>
/// <param name="connection">The server's end of the connection.</param>
internal sealed class InMemorySshServerSession(InMemorySshServer server, InMemoryDuplexConnection connection)
{
    /// <summary>The server's number for its channel.</summary>
    internal const uint ServerChannel = 7;

    private const string KeyExchangeMethod = "diffie-hellman-group14-sha256";

    private const int ScpChunkSize = 16384;

    private static readonly byte[] ScpDownloadPrefix = "scp -pf "u8.ToArray();

    private static readonly byte[] ScpUploadPrefix = "scp -t "u8.ToArray();

    private readonly SshConnectionReader connectionReader = new(connection);

    private readonly MemoryStream sftpInput = new();

    private readonly MemoryStream scpInput = new();

    private string? scpUploadPath;

    private readonly HashSet<string> listedDirectories = new(StringComparer.Ordinal);

    private SshPacketReader? packetReader;

    private SshPacketWriter? packetWriter;

    private byte[] sessionIdentifier = [];

    private uint clientChannel;

    private bool closeSent;

    private bool isCompressionDelayed;

    private SshPacketReader Reader => packetReader!;

    private SshPacketWriter Writer => packetWriter!;

    /// <summary>
    /// Runs the session until the client disconnects or closes, then closes the server's end.
    /// </summary>
    /// <returns>A task that completes when the session has ended.</returns>
    internal async Task RunAsync()
    {
        try
        {
            await ExchangeKeysAsync().ConfigureAwait(false);
            while (await AnswerNextMessageAsync().ConfigureAwait(false))
            {
            }
        }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or HangUp)
        {
            server.Record("closed");
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static byte[] Unquote(ReadOnlySpan<byte> quoted)
    {
        List<byte> path = [];
        for (int index = 0; index < quoted.Length; index++)
        {
            byte current = quoted[index];
            if (current is (byte)'\'' or (byte)'"')
            {
                int end = quoted[(index + 1)..].IndexOf(current) + index + 1;
                path.AddRange(quoted[(index + 1)..end]);
                index = end;
            }
            else
            {
                index += current == (byte)'\\' ? 1 : 0;
                path.Add(quoted[index]);
            }
        }

        return [.. path];
    }

    private async Task ExchangeKeysAsync()
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes(InMemorySshServer.Identification + "\r\n"), CancellationToken.None).ConfigureAwait(false);
        string clientIdentification = await connectionReader.ReadLineAsync(SshIdentificationExchange.MaximumLineLength, CancellationToken.None).ConfigureAwait(false)
            ?? throw new EndOfStreamException();
        packetReader = new SshPacketReader(connectionReader);
        packetWriter = new SshPacketWriter(connection, new SystemSshRandomSource());
        byte[] serverKexInit = new SshKexInit(
            RandomNumberGenerator.GetBytes(16), [KeyExchangeMethod], [server.HostKey.Algorithm], [server.Cipher], [server.Cipher], [server.Mac], [server.Mac], [server.Compression], [server.Compression], [], [], FirstKexPacketFollows: false).ToPayload();
        await SendAsync(serverKexInit).ConfigureAwait(false);
        byte[] clientKexInit = await Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        SshNegotiatedAlgorithms algorithms = SshAlgorithmNegotiator.Negotiate(SshKexInit.Parse(clientKexInit), SshKexInit.Parse(serverKexInit), [])!;
        byte[] clientPublicValue = new SshWireReader(await Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false)).Skip(1).ReadMpint().ToArray();
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group14;
        using FiniteFieldDiffieHellman key = FiniteFieldDiffieHellman.Generate(group);
        byte[] f = new byte[group.PrimeLength];
        byte[] k = new byte[group.PrimeLength];
        byte[] e = new byte[group.PrimeLength];
        clientPublicValue.CopyTo(e, group.PrimeLength - clientPublicValue.Length);
        key.ComputePublicValue(f);
        key.TryComputeSharedSecret(e, k);
        byte[] h = SHA256.HashData(Join(
            Name(clientIdentification), Name(InMemorySshServer.Identification), String(clientKexInit), String(serverKexInit), String(server.HostKeyBlob), Mpint(e), Mpint(f), Mpint(k)));
        await SendAsync(TestKeyExchangeServer.FiniteFieldReply(31, server.HostKeyBlob, f, server.HostKey.Sign(h))).ConfigureAwait(false);
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, Mpint(k), h, h);
        await SendAsync([SshMessageNumber.NewKeys]).ConfigureAwait(false);
        Writer.ChangeProtection(SshPacketProtections.ForServerToClient(algorithms, keys));
        await Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        Reader.ChangeProtection(SshPacketProtections.ForClientToServer(algorithms, keys));
        sessionIdentifier = h;
        if (server.Compression == SshCompressionMethods.Zlib)
        {
            StartCompression();
        }

        isCompressionDelayed = server.Compression == SshCompressionMethods.DelayedZlib;
    }

    private async Task<bool> AnswerNextMessageAsync()
    {
        byte[] payload = await Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        SshWireReader message = new SshWireReader(payload).Skip(1);
        switch (payload[0])
        {
            case SshMessageNumber.Disconnect:
                uint reason = message.ReadUInt32();
                server.Record($"disconnect {reason} {message.ReadName()}");
                return false;
            case SshMessageNumber.ServiceRequest:
                server.Record($"service {message.ReadName()}");
                await SendAsync([SshMessageNumber.ServiceAccept, .. Name(SshUserAuthentication.UserAuthService)]).ConfigureAwait(false);
                break;
            case SshAuthenticationMessageNumber.Request:
                await AnswerAuthenticationAsync(payload, message).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelOpen:
                await AnswerChannelOpenAsync(message).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelRequest:
                await AnswerChannelRequestAsync(message).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelData when scpUploadPath is not null:
                await ReceiveScpAsync(message.Skip(4).ReadString()).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelData:
                await AnswerSftpAsync(message.Skip(4).ReadString()).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelEof when scpUploadPath is not null:
                StoreScpUpload();
                server.Record("channel eof");
                await SendCloseAsync().ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelEof:
                server.Record("channel eof");
                await SendCloseAsync().ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelClose:
                server.Record("channel close");
                break;
        }

        return true;
    }

    private async Task AnswerAuthenticationAsync(byte[] payload, SshWireReader message)
    {
        byte[] user = message.ReadString().ToArray();
        message.ReadName();
        string method = message.ReadName();
        bool? succeeded = method switch
        {
            SshUserAuthentication.PasswordMethod => server.IsPassword(user, message.Skip(1).ReadString().ToArray()),
            SshUserAuthentication.PublicKeyMethod => await AnswerPublicKeyAsync(payload, message).ConfigureAwait(false),
            _ => false,
        };
        if (succeeded is { } outcome)
        {
            server.Record($"auth {method} {Encoding.UTF8.GetString(user)} {(outcome ? "ok" : "refused")}");
            await SendAsync(outcome ? [SshAuthenticationMessageNumber.Success] : [SshAuthenticationMessageNumber.Failure, .. Name("publickey,password"), 0]).ConfigureAwait(false);
            if (outcome && isCompressionDelayed)
            {
                StartCompression();
            }
        }
    }

    // Both directions at once: the server's packets after this point, and the client's,
    // which it compresses from the same point.
    private void StartCompression()
    {
        Writer.StartCompression();
        Reader.StartDecompression();
    }

    // Null once PK_OK has answered the question; the signature's verdict otherwise.
    private async Task<bool?> AnswerPublicKeyAsync(byte[] payload, SshWireReader message)
    {
        bool signed = message.ReadBoolean();
        string algorithm = message.ReadName();
        byte[] blob = message.ReadString().ToArray();
        if (server.AuthorizedPublicKey is not { } authorized || !blob.AsSpan().SequenceEqual(authorized))
        {
            return false;
        }

        if (!signed)
        {
            await SendAsync([SshAuthenticationMessageNumber.PublicKeyOk, .. Name(algorithm), .. String(blob)]).ConfigureAwait(false);
            return null;
        }

        byte[] signature = message.ReadString().ToArray();
        byte[] signedData = Join(String(sessionIdentifier), payload[..^(signature.Length + 4)]);
        return SshSignatureVerifiers.For(algorithm).Verify(blob, signature, signedData);
    }

    private async Task AnswerChannelOpenAsync(SshWireReader message)
    {
        server.Record($"channel open {message.ReadName()}");
        clientChannel = message.ReadUInt32();
        if (server.HangsUpOnChannelOpen)
        {
            throw new HangUp();
        }

        await SendAsync(server.RefusesSessionChannels
            ? Join([SshConnectionMessageNumber.ChannelOpenFailure], UInt32(clientChannel), UInt32(2), Name("open failed"), Name(string.Empty))
            : Join([SshConnectionMessageNumber.ChannelOpenConfirmation], UInt32(clientChannel), UInt32(ServerChannel), UInt32(2 * 1024 * 1024), UInt32(32768))).ConfigureAwait(false);
    }

    private async Task AnswerChannelRequestAsync(SshWireReader message)
    {
        string type = message.Skip(4).ReadName();
        message.ReadBoolean();
        byte[] argument = message.ReadString().ToArray();
        server.Record($"{type} {Encoding.Latin1.GetString(argument)}");
        await SendAsync([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(clientChannel)]).ConfigureAwait(false);
        if (type == "exec" && argument.AsSpan().StartsWith(ScpUploadPrefix))
        {
            scpUploadPath = Encoding.UTF8.GetString(Unquote(argument.AsSpan(ScpUploadPrefix.Length)));
            await SendChannelDataAsync([0]).ConfigureAwait(false);
        }
        else if (type == "exec")
        {
            await RunScpAsync(Encoding.UTF8.GetString(Unquote(argument.AsSpan(ScpDownloadPrefix.Length)))).ConfigureAwait(false);
        }
    }

    // What OpenSSH's scp -t does: acknowledges the C line once it has arrived, then takes
    // the bytes after it until the client's EOF.
    private async Task ReceiveScpAsync(ReadOnlyMemory<byte> data)
    {
        bool lineWasComplete = scpInput.ToArray().Contains((byte)'\n');
        scpInput.Write(data.Span);
        if (!lineWasComplete && scpInput.ToArray().Contains((byte)'\n'))
        {
            await SendChannelDataAsync([0]).ConfigureAwait(false);
        }
    }

    private void StoreScpUpload()
    {
        byte[] received = scpInput.ToArray();
        int lineEnd = Array.IndexOf(received, (byte)'\n');
        server.Record($"scp {Encoding.Latin1.GetString(received, 0, lineEnd)}");
        server.Files[scpUploadPath!] = received[(lineEnd + 1)..];
    }

    // What OpenSSH's scp -pf sends: the times, the file line, the bytes and a zero byte, or
    // an error line; then the channel's end.
    private async Task RunScpAsync(string path)
    {
        if (server.Files.TryGetValue(path, out byte[]? file))
        {
            await SendChannelDataAsync(Encoding.ASCII.GetBytes($"T1790702112 0 1790702112 0\nC0644 {file.Length} {path.Split('/')[^1]}\n")).ConfigureAwait(false);
            foreach (byte[] chunk in file.Chunk(ScpChunkSize))
            {
                await SendChannelDataAsync(chunk).ConfigureAwait(false);
            }

            await SendChannelDataAsync([0]).ConfigureAwait(false);
        }
        else
        {
            await SendChannelDataAsync(Encoding.UTF8.GetBytes($"\u0001scp: {path}: No such file or directory\n")).ConfigureAwait(false);
        }

        await SendAsync([SshConnectionMessageNumber.ChannelEof, .. UInt32(clientChannel)]).ConfigureAwait(false);
        await SendCloseAsync().ConfigureAwait(false);
    }

    private async Task AnswerSftpAsync(ReadOnlyMemory<byte> data)
    {
        sftpInput.Write(data.Span);
        byte[] buffered = sftpInput.ToArray();
        int position = 0;
        while (buffered.Length - position >= 4 && new SshWireReader(buffered.AsMemory(position)).ReadUInt32() is uint length && buffered.Length - position - 4 >= length)
        {
            await AnswerSftpPacketAsync(buffered.AsMemory(position + 4, (int)length)).ConfigureAwait(false);
            position += 4 + (int)length;
        }

        sftpInput.SetLength(0);
        sftpInput.Write(buffered.AsSpan(position));
    }

    private async Task AnswerSftpPacketAsync(ReadOnlyMemory<byte> packet)
    {
        byte type = packet.Span[0];
        SshWireReader request = new SshWireReader(packet).Skip(1);
        if (type == SftpPacketType.Init)
        {
            await SendSftpAsync([SftpPacketType.Version, .. UInt32(3)]).ConfigureAwait(false);
            return;
        }

        uint id = request.ReadUInt32();
        string argument = Encoding.UTF8.GetString(request.ReadString().Span);
        server.Record($"sftp {type} {argument}");
        byte[]? file = FileFor(type, argument, request);
        byte[] answer = type switch
        {
            SftpPacketType.RealPath => Join([SftpPacketType.Name], UInt32(id), UInt32(1), Name(server.HomeDirectory), Name(server.HomeDirectory), UInt32(0)),
            SftpPacketType.Write => StoreWrite(id, argument, request),
            SftpPacketType.Open or SftpPacketType.Stat when file is null => Status(id, 2),
            SftpPacketType.Open => Join([SftpPacketType.Handle], UInt32(id), Name(argument)),
            SftpPacketType.Stat => Join([SftpPacketType.Attributes], UInt32(id), UInt32(1), UInt32(0), UInt32((uint)file!.Length)),
            SftpPacketType.Read => ReadAnswer(id, file!, request.ReadBytes(8), request.ReadUInt32()),
            SftpPacketType.OpenDirectory => FilesIn(argument).Any() ? Join([SftpPacketType.Handle], UInt32(id), Name(argument)) : Status(id, 2),
            SftpPacketType.ReadDirectory => ReadDirectoryAnswer(id, argument),
            _ => Status(id, 0),
        };
        await SendSftpAsync(answer).ConfigureAwait(false);
    }

    // The file a request names; an open with SSH_FXF_CREAT creates a missing one, empty.
    private byte[]? FileFor(byte type, string path, SshWireReader request)
    {
        if (server.Files.TryGetValue(path, out byte[]? found))
        {
            return found;
        }

        return type == SftpPacketType.Open && (request.ReadUInt32() & SftpOpenFlags.Create) != 0 ? server.Files[path] = [] : null;
    }

    // Writes the bytes at their offset of the file whose handle is its path, growing it.
    private byte[] StoreWrite(uint id, string path, SshWireReader request)
    {
        long offset = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(request.ReadBytes(8).Span);
        ReadOnlySpan<byte> data = request.ReadString().Span;
        byte[] file = server.Files[path];
        byte[] written = new byte[Math.Max(file.Length, offset + data.Length)];
        file.CopyTo(written, 0);
        data.CopyTo(written.AsSpan((int)offset));
        server.Files[path] = written;
        return Status(id, 0);
    }

    private static byte[] ReadAnswer(uint id, byte[] file, ReadOnlyMemory<byte> offsetBytes, uint length)
    {
        long offset = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(offsetBytes.Span);
        return offset >= file.Length
            ? Status(id, 1)
            : Join([SftpPacketType.Data], UInt32(id), String(file.AsSpan((int)offset, (int)Math.Min(length, file.Length - offset)).ToArray()));
    }

    // The files directly under directory, a path ending with a slash.
    private IEnumerable<KeyValuePair<string, byte[]>> FilesIn(string directory) =>
        server.Files.Where(file => file.Key.StartsWith(directory, StringComparison.Ordinal) && !file.Key[directory.Length..].Contains('/'));

    // Every file of the directory in one SSH_FXP_NAME, each a regular file 0644 with an
    // ls -l-style long name, then SSH_FX_EOF.
    private byte[] ReadDirectoryAnswer(uint id, string directory)
    {
        if (!listedDirectories.Add(directory))
        {
            return Status(id, 1);
        }

        KeyValuePair<string, byte[]>[] files = [.. FilesIn(directory).OrderBy(file => file.Key, StringComparer.Ordinal)];
        byte[] names = [.. files.SelectMany(file => Join(
            Name(file.Key[directory.Length..]),
            Name($"-rw-r--r--    1 {server.UserName} {server.UserName} {file.Value.Length,8} Jan  1  2026 {file.Key[directory.Length..]}"),
            UInt32(4),
            UInt32(0x81A4)))];
        return Join([SftpPacketType.Name], UInt32(id), UInt32((uint)files.Length), names);
    }

    private static byte[] Status(uint id, uint code) =>
        Join([SftpPacketType.Status], UInt32(id), UInt32(code), Name("status"), Name(string.Empty));

    private Task SendSftpAsync(byte[] packet) => SendChannelDataAsync([.. UInt32((uint)packet.Length), .. packet]);

    private Task SendChannelDataAsync(byte[] data) =>
        SendAsync(Join([SshConnectionMessageNumber.ChannelData], UInt32(clientChannel), String(data)));

    private async Task SendCloseAsync()
    {
        if (!closeSent)
        {
            closeSent = true;
            await SendAsync([SshConnectionMessageNumber.ChannelClose, .. UInt32(clientChannel)]).ConfigureAwait(false);
        }
    }

    // A client that has already hung up misses what follows, but what it sent before is
    // still read, as a socket's receive buffer keeps it.
    private async Task SendAsync(byte[] payload)
    {
        try
        {
            await Writer.WriteAsync(payload, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    // Ends the session at once, as a server that drops the connection does.
    private sealed class HangUp : Exception;
}

/// <summary>
/// Lets the in-memory server step over fields it does not look at.
/// </summary>
internal static class SshWireReaderSkipping
{
    /// <summary>Reads and drops <paramref name="count" /> bytes.</summary>
    /// <returns>The same reader.</returns>
    internal static SshWireReader Skip(this SshWireReader reader, int count)
    {
        reader.ReadBytes(count);
        return reader;
    }
}
