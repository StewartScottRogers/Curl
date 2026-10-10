using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// OpenSSH's <c>sftp-server</c> as upstream's test <c>sshd</c> runs it for the <c>sftp</c>
/// subsystem (BL-1918): SFTP protocol version 3 on the real files the case names under its log
/// directory. It answers <c>INIT</c> with <c>VERSION 3</c> and no extensions, and then <c>OPEN</c>,
/// <c>CLOSE</c>, <c>READ</c>, <c>WRITE</c>, <c>STAT</c>, <c>LSTAT</c>, <c>FSTAT</c>, <c>SETSTAT</c>,
/// <c>FSETSTAT</c>, <c>OPENDIR</c>, <c>READDIR</c>, <c>REMOVE</c>, <c>MKDIR</c>, <c>RMDIR</c>,
/// <c>REALPATH</c> and <c>RENAME</c>, each in the order it arrives; anything else is
/// <c>SSH_FX_OP_UNSUPPORTED</c>. A missing file or folder is <c>SSH_FX_NO_SUCH_FILE</c>, a refused
/// one <c>SSH_FX_PERMISSION_DENIED</c> and any other failure <c>SSH_FX_FAILURE</c>, as
/// <c>sftp-server</c> maps <c>errno</c> for version 3. <c>SETSTAT</c> changes nothing: the
/// stand-in's files carry no Unix owner or mode on Windows.
/// </summary>
internal sealed class SshServerSftpProcess : IDisposable
{
    /// <summary>The subsystem name a client requests.</summary>
    internal const string SubsystemName = "sftp";

    private const byte Init = 1;
    private const byte Version = 2;
    private const byte Status = 101;
    private const byte Handle = 102;
    private const byte Data = 103;
    private const byte Name = 104;
    private const byte Attributes = 105;

    private const uint Ok = 0;
    private const uint EndOfFile = 1;
    private const uint NoSuchFile = 2;
    private const uint PermissionDenied = 3;
    private const uint Failure = 4;
    private const uint OperationUnsupported = 8;

    private const uint OpenRead = 0x01;
    private const uint OpenWrite = 0x02;
    private const uint OpenAppend = 0x04;
    private const uint OpenCreate = 0x08;
    private const uint OpenTruncate = 0x10;
    private const uint OpenExclusive = 0x20;

    // Size, owner and group, permissions, access and modify times.
    private const uint AttributeFlags = 0x0F;
    private const uint DirectoryPermissions = 0x41ED;
    private const uint FilePermissions = 0x81A4;

    // sftp-server reads at most this much for one READ.
    private const int MaximumReadLength = 256 * 1024;

    private static readonly string[] StatusMessages = ["Success", "End of file", "No such file", "Permission denied", "Failure"];

    private readonly Dictionary<string, FileStream> files = new(StringComparer.Ordinal);

    // Each open directory's path, and whether READDIR has listed it yet.
    private readonly Dictionary<string, (string Path, bool Listed)> directories = new(StringComparer.Ordinal);

    private readonly Dictionary<byte, Func<SshWireReader, byte[]>> requests;

    private readonly string user;

    private int nextHandle;

    internal SshServerSftpProcess(string user)
    {
        this.user = user;
        requests = new()
        {
            [3] = OpenFile,
            [4] = CloseHandle,
            [5] = ReadFile,
            [6] = WriteFile,
            [7] = StatPath,
            [8] = StatHandle,
            [9] = SetStatPath,
            [10] = SetStatHandle,
            [11] = OpenDirectory,
            [12] = ReadDirectory,
            [13] = RemoveFile,
            [14] = MakeDirectory,
            [15] = RemoveDirectory,
            [16] = RealPath,
            [17] = StatPath,
            [18] = Rename,
        };
    }

    /// <summary>
    /// Serves SFTP on <paramref name="channel"/> until the client sends <c>EOF</c> or closes it,
    /// then closes the channel with exit status 0.
    /// </summary>
    /// <param name="channel">The channel the <c>subsystem</c> request started.</param>
    /// <param name="cancellationToken">Cancels the process.</param>
    /// <returns>The exit status, 0.</returns>
    internal static async ValueTask<uint> RunAsync(SshServerSessionChannel channel, CancellationToken cancellationToken)
    {
        SshServerChannelInput input = new(channel);
        using (SshServerSftpProcess process = new(channel.User))
        {
            while (await ReadPacketAsync(input, cancellationToken).ConfigureAwait(false) is { } packet)
            {
                byte[] answer = process.Answer(packet);
                byte[] framed = new byte[4 + answer.Length];
                BinaryPrimitives.WriteUInt32BigEndian(framed, (uint)answer.Length);
                answer.CopyTo(framed, 4);
                await channel.WriteDataAsync(framed, cancellationToken).ConfigureAwait(false);
            }
        }

        await channel.CloseAsync(0, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// Answers one request packet, without its length.
    /// </summary>
    /// <param name="packet">The request: its type, then (for all but <c>INIT</c>) its identifier and fields.</param>
    /// <returns>The reply packet, without its length.</returns>
    internal byte[] Answer(byte[] packet)
    {
        SshWireReader request = new(packet);
        byte type = request.ReadByte();
        if (type == Init)
        {
            return [Version, 0, 0, 0, 3];
        }

        uint id = request.ReadUInt32();
        try
        {
            return Reply(id, requests.TryGetValue(type, out Func<SshWireReader, byte[]>? answer) ? answer(request) : StatusBody(OperationUnsupported));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Reply(id, StatusBody(StatusFor(exception)));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (FileStream file in files.Values)
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// The path a client names, as this machine opens it (see <see cref="SshServerScpProcess.LocalPath"/>).
    /// </summary>
    /// <param name="path">The path's bytes from the request.</param>
    /// <returns>The path to open.</returns>
    internal static string LocalPath(ReadOnlyMemory<byte> path) => SshServerScpProcess.LocalPath(Encoding.UTF8.GetString(path.Span));

    /// <summary>
    /// The absolute path a client is told for a local one: forward slashes, with a slash before a
    /// Windows drive, as an <c>sftp://</c> URL names it.
    /// </summary>
    /// <param name="localPath">A full local path.</param>
    /// <returns>The client's path.</returns>
    internal static string ClientPath(string localPath)
    {
        string slashed = localPath.Replace('\\', '/');
        return slashed.StartsWith('/') ? slashed : "/" + slashed;
    }

    /// <summary>
    /// The <c>ls -l</c> line <c>sftp-server</c> sends as an entry's long name.
    /// </summary>
    /// <param name="path">The entry's local path.</param>
    /// <param name="name">The entry's name.</param>
    /// <param name="owner">The owner and group to show.</param>
    /// <param name="now">The time to judge the entry's age against.</param>
    /// <returns>The long name.</returns>
    internal static string LongName(string path, string name, string owner, DateTime now)
    {
        bool isDirectory = Directory.Exists(path);
        long size = isDirectory ? 0 : new FileInfo(path).Length;
        DateTime modified = File.GetLastWriteTimeUtc(path);
        string time = Math.Abs((now - modified).TotalDays) < 182
            ? modified.ToString("MMM d HH:mm", CultureInfo.InvariantCulture)
            : modified.ToString("MMM d  yyyy", CultureInfo.InvariantCulture);
        string padded = owner.PadRight(8);
        return string.Create(CultureInfo.InvariantCulture, $"{(isDirectory ? "drwxr-xr-x" : "-rw-r--r--")}   1 {padded} {padded} {size,8} {time,12} {name}");
    }

    private static async ValueTask<byte[]?> ReadPacketAsync(SshServerChannelInput input, CancellationToken cancellationToken)
    {
        byte[] length = await input.ReadBlockAsync(4, cancellationToken).ConfigureAwait(false);
        return length.Length < 4
            ? null
            : await input.ReadBlockAsync(BinaryPrimitives.ReadUInt32BigEndian(length), cancellationToken).ConfigureAwait(false);
    }

    internal static uint StatusFor(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => NoSuchFile,
        UnauthorizedAccessException => PermissionDenied,
        _ => Failure,
    };

    private static byte[] Reply(uint id, byte[] body)
    {
        byte[] reply = new byte[body.Length + 4];
        reply[0] = body[0];
        BinaryPrimitives.WriteUInt32BigEndian(reply.AsSpan(1), id);
        body.AsSpan(1).CopyTo(reply.AsSpan(5));
        return reply;
    }

    // A reply's type and fields; Reply puts the request's identifier after the type.
    private static byte[] Body(byte type, Action<SshWireWriter> writeFields)
    {
        SshWireWriter body = new();
        body.WriteByte(type);
        writeFields(body);
        return body.ToArray();
    }

    private static byte[] StatusBody(uint code) => Body(Status, body =>
    {
        body.WriteUInt32(code);
        body.WriteString(Encoding.ASCII.GetBytes(code < StatusMessages.Length ? StatusMessages[code] : "Operation unsupported"));
        body.WriteString([]);
    });

    private static void WriteAttributes(SshWireWriter body, string path)
    {
        bool isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            throw new FileNotFoundException(null, path);
        }

        body.WriteUInt32(AttributeFlags);
        long size = isDirectory ? 0 : new FileInfo(path).Length;
        body.WriteUInt32((uint)(size >> 32));
        body.WriteUInt32((uint)size);
        body.WriteUInt32(0);
        body.WriteUInt32(0);
        body.WriteUInt32(isDirectory ? DirectoryPermissions : FilePermissions);
        body.WriteUInt32((uint)new DateTimeOffset(File.GetLastAccessTimeUtc(path)).ToUnixTimeSeconds());
        body.WriteUInt32((uint)new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds());
    }

    private static byte[] AttributesBody(string path) => Body(Attributes, body => WriteAttributes(body, path));

    private static FileMode OpenMode(uint flags) =>
        (flags & OpenCreate) == 0 ? FileMode.Open
        : (flags & OpenExclusive) != 0 ? FileMode.CreateNew
        : (flags & OpenTruncate) != 0 ? FileMode.Create
        : FileMode.OpenOrCreate;

    private static FileAccess OpenAccess(uint flags) =>
        (flags & (OpenWrite | OpenAppend)) == 0 ? FileAccess.Read
        : (flags & OpenRead) == 0 ? FileAccess.Write
        : FileAccess.ReadWrite;

    private byte[] HandleBody(string handle) => Body(Handle, body => body.WriteString(Encoding.ASCII.GetBytes(handle)));

    private string NewHandle() => (nextHandle++).ToString(CultureInfo.InvariantCulture);

    private byte[] OpenFile(SshWireReader request)
    {
        string path = LocalPath(request.ReadString());
        uint flags = request.ReadUInt32();
        if (Directory.Exists(path))
        {
            throw new IOException($"{path} is a directory");
        }

        string handle = NewHandle();
        files[handle] = new FileStream(path, OpenMode(flags), OpenAccess(flags), FileShare.ReadWrite | FileShare.Delete);
        return HandleBody(handle);
    }

    private FileStream OpenedFile(SshWireReader request) =>
        files.TryGetValue(Encoding.ASCII.GetString(request.ReadString().Span), out FileStream? file)
            ? file
            : throw new IOException("invalid handle");

    private byte[] CloseHandle(SshWireReader request)
    {
        string handle = Encoding.ASCII.GetString(request.ReadString().Span);
        if (files.Remove(handle, out FileStream? file))
        {
            file.Dispose();
        }
        else if (!directories.Remove(handle))
        {
            throw new IOException("invalid handle");
        }

        return StatusBody(Ok);
    }

    private byte[] ReadFile(SshWireReader request)
    {
        FileStream file = OpenedFile(request);
        file.Position = (long)((ulong)request.ReadUInt32() << 32 | request.ReadUInt32());
        byte[] buffer = new byte[Math.Min(request.ReadUInt32(), MaximumReadLength)];
        int read = file.Read(buffer);
        return read == 0 ? StatusBody(EndOfFile) : Body(Data, body => body.WriteString(buffer.AsSpan(0, read)));
    }

    private byte[] WriteFile(SshWireReader request)
    {
        FileStream file = OpenedFile(request);
        file.Position = (long)((ulong)request.ReadUInt32() << 32 | request.ReadUInt32());
        file.Write(request.ReadString().Span);
        file.Flush();
        return StatusBody(Ok);
    }

    private byte[] StatPath(SshWireReader request) => AttributesBody(LocalPath(request.ReadString()));

    private byte[] StatHandle(SshWireReader request) => AttributesBody(OpenedFile(request).Name);

    private byte[] SetStatPath(SshWireReader request) => SetStat(LocalPath(request.ReadString()));

    private byte[] SetStatHandle(SshWireReader request) => SetStat(OpenedFile(request).Name);

    private static byte[] SetStat(string path)
    {
        WriteAttributes(new SshWireWriter(), path);
        return StatusBody(Ok);
    }

    private byte[] OpenDirectory(SshWireReader request)
    {
        string path = LocalPath(request.ReadString());
        if (!Directory.Exists(path))
        {
            throw File.Exists(path) ? new IOException($"{path} is not a directory") : new DirectoryNotFoundException(path);
        }

        string handle = NewHandle();
        directories[handle] = (path, false);
        return HandleBody(handle);
    }

    // The whole listing, "." and ".." first and the rest in ordinal order, then end of file.
    private byte[] ReadDirectory(SshWireReader request)
    {
        string handle = Encoding.ASCII.GetString(request.ReadString().Span);
        if (!directories.TryGetValue(handle, out (string Path, bool Listed) directory))
        {
            throw new IOException("invalid handle");
        }

        if (directory.Listed)
        {
            return StatusBody(EndOfFile);
        }

        directories[handle] = (directory.Path, true);
        string[] names = [".", "..", .. Directory.EnumerateFileSystemEntries(directory.Path).Select(System.IO.Path.GetFileName).Order(StringComparer.Ordinal)!];
        DateTime now = DateTime.UtcNow;
        return Body(Name, body =>
        {
            body.WriteUInt32((uint)names.Length);
            foreach (string name in names)
            {
                string path = System.IO.Path.Combine(directory.Path, name);
                body.WriteString(Encoding.UTF8.GetBytes(name));
                body.WriteString(Encoding.UTF8.GetBytes(LongName(path, name, user, now)));
                WriteAttributes(body, path);
            }
        });
    }

    private static byte[] RemoveFile(SshWireReader request)
    {
        string path = LocalPath(request.ReadString());
        if (!File.Exists(path))
        {
            throw Directory.Exists(path) ? new IOException($"{path} is a directory") : new FileNotFoundException(null, path);
        }

        File.Delete(path);
        return StatusBody(Ok);
    }

    private static byte[] MakeDirectory(SshWireReader request)
    {
        string path = System.IO.Path.GetFullPath(LocalPath(request.ReadString()));
        if (Directory.Exists(path) || File.Exists(path))
        {
            throw new IOException($"{path} exists");
        }

        if (!Directory.Exists(System.IO.Path.GetDirectoryName(path)))
        {
            throw new DirectoryNotFoundException(path);
        }

        Directory.CreateDirectory(path);
        return StatusBody(Ok);
    }

    private static byte[] RemoveDirectory(SshWireReader request)
    {
        Directory.Delete(LocalPath(request.ReadString()));
        return StatusBody(Ok);
    }

    // "." (or nothing) is the current directory, as sftp-server starts in the user's home.
    private static byte[] RealPath(SshWireReader request)
    {
        string path = LocalPath(request.ReadString());
        string full = ClientPath(System.IO.Path.GetFullPath(path.Length == 0 ? "." : path));
        return Body(Name, body =>
        {
            body.WriteUInt32(1);
            body.WriteString(Encoding.UTF8.GetBytes(full));
            body.WriteString(Encoding.UTF8.GetBytes(full));
            body.WriteUInt32(0);
        });
    }

    // Version 3's RENAME fails when the new path exists, as File.Move and Directory.Move do.
    private static byte[] Rename(SshWireReader request)
    {
        string from = LocalPath(request.ReadString());
        string to = LocalPath(request.ReadString());
        if (File.Exists(from))
        {
            File.Move(from, to);
        }
        else
        {
            Directory.Move(from, to);
        }

        return StatusBody(Ok);
    }
}
