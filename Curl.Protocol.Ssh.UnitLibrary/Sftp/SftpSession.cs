using System.Buffers.Binary;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The client side of an SFTP version 3 session (draft-ietf-secsh-filexfer-02) over one
/// session channel, as curl 8.21.0 drives libssh2 1.11.1 through it (ADR-0220): the
/// packets are framed with a four-byte length, requests are numbered from 0, and an
/// answer is matched to its request by that number, every other answer being skipped.
/// </summary>
internal sealed class SftpSession
{
    /// <summary>The SFTP version the client sends in <c>SSH_FXP_INIT</c>.</summary>
    internal const uint ProtocolVersion = 3;

    /// <summary>The longest packet the client accepts, libssh2's <c>LIBSSH2_SFTP_PACKET_MAXLEN</c>.</summary>
    internal const int MaximumPacketLength = 256 * 1024;

    /// <summary>The subsystem the channel starts.</summary>
    internal const string SubsystemName = "sftp";

    /// <summary>libssh2's description when the channel cannot be opened.</summary>
    internal const string UnableToStartupChannel = "Unable to startup channel";

    /// <summary>libssh2's description when the server refuses the subsystem.</summary>
    internal const string UnableToRequestSubsystem = "Unable to request SFTP subsystem";

    /// <summary>libssh2's description when the connection ends before <c>SSH_FXP_VERSION</c>.</summary>
    internal const string TimeoutWaitingForVersion = "Timeout waiting for response from SFTP subsystem";

    /// <summary>libssh2's description when a <c>SSH_FXP_VERSION</c> extension's name is cut short.</summary>
    internal const string ExtensionNameTooShort = "Data too short when extracting extname";

    /// <summary>libssh2's description when a <c>SSH_FXP_VERSION</c> extension's data is cut short.</summary>
    internal const string ExtensionDataTooShort = "Data too short when extracting extdata";

    // SSH_FXF_READ, the attribute flags, and the file-type bits libssh2 sends with an open
    // and reads from a directory's entries.
    private const uint OpenForReading = 0x00000001;

    private const uint AttributeSize = 0x00000001;

    private const uint AttributeUserAndGroup = 0x00000002;

    private const uint AttributePermissions = 0x00000004;

    private const uint AttributeAccessAndModifyTimes = 0x00000008;

    private const uint AttributeExtended = 0x80000000;

    private const uint FileTypeMask = 0xF000;

    private const uint RegularFileType = 0x8000;

    private const uint SymbolicLinkType = 0xA000;

    private readonly SshSessionChannel channel;

    private uint nextRequestId;

    private SftpSession(SshSessionChannel channel) => this.channel = channel;

    /// <summary>
    /// Opens a session channel, starts the <c>sftp</c> subsystem and exchanges
    /// <c>SSH_FXP_INIT</c> and <c>SSH_FXP_VERSION</c>. Any version the server names is
    /// accepted, and an answer too short to be a <c>VERSION</c>, or of another type, is
    /// skipped, as measured.
    /// </summary>
    /// <param name="transport">The transport, after the user is authenticated.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The session.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 2, <c>Failure initializing sftp session: </c> and <c>Unable to startup
    /// channel</c> when the channel is refused, <c>Unable to request SFTP subsystem</c> when
    /// the subsystem is, <c>Timeout waiting for response from SFTP subsystem</c> when the
    /// connection ends first, and <c>Data too short when extracting extname</c> or
    /// <c>extdata</c> for a cut-short extension.
    /// </exception>
    internal static async ValueTask<SftpSession> StartAsync(SshTransport transport, CancellationToken cancellationToken)
    {
        SshSessionChannel channel = new(transport);
        await RequireAsync(() => channel.OpenAsync(cancellationToken), UnableToStartupChannel).ConfigureAwait(false);
        await RequireAsync(() => channel.RequestSubsystemAsync(SubsystemName, cancellationToken), UnableToRequestSubsystem).ConfigureAwait(false);
        SftpSession session = new(channel);
        byte[] version = [];
        await RequireAsync(
            async () =>
            {
                version = await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
                return true;
            },
            TimeoutWaitingForVersion).ConfigureAwait(false);
        CheckExtensions(version);
        return session;
    }

    /// <summary>
    /// Sends <c>SSH_FXP_REALPATH</c> for <paramref name="path" /> and reads the first name
    /// of the answer.
    /// </summary>
    /// <param name="path">The path, such as <c>.</c> for the home directory.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The absolute path, as the server sent its bytes.</returns>
    /// <exception cref="SshTransferException">The server answered with a status: its exit code and that exit code's text.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<byte[]> RealPathAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.RealPath, fields => fields.WriteString(path), cancellationToken).ConfigureAwait(false);
        SshWireReader answer = await ReadAnswerAsync(id, SftpPacketType.Name, cancellationToken).ConfigureAwait(false);
        answer.ReadUInt32();
        return answer.ReadString().ToArray();
    }

    /// <summary>
    /// Sends <c>SSH_FXP_OPEN</c> for reading, with the permissions attribute libssh2 adds:
    /// a regular file with <paramref name="createFileMode" />. A status of
    /// <c>SSH_FX_OK</c> is not an answer: as measured, curl goes on waiting.
    /// </summary>
    /// <param name="path">The path to open.</param>
    /// <param name="createFileMode">The permission bits, curl's <c>--create-file-mode</c>, 0644 unless given.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The file's handle.</returns>
    /// <exception cref="SshTransferException">The server answered with a failed status: <see cref="SshTransferException.SftpOpenFailed" />.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<byte[]> OpenForReadingAsync(byte[] path, UnixFileMode createFileMode, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(
            SftpPacketType.Open,
            fields =>
            {
                fields.WriteString(path);
                fields.WriteUInt32(OpenForReading);
                fields.WriteUInt32(AttributePermissions);
                fields.WriteUInt32(RegularFileType | (uint)createFileMode);
            },
            cancellationToken).ConfigureAwait(false);
        return await ReadHandleAsync(id, SshTransferException.SftpOpenFailed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_OPENDIR</c> for <paramref name="path" />. A status of
    /// <c>SSH_FX_OK</c> is not an answer: as measured, curl goes on waiting.
    /// </summary>
    /// <param name="path">The directory's path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The directory's handle.</returns>
    /// <exception cref="SshTransferException">The server answered with a failed status: <see cref="SshTransferException.SftpOpenDirectoryFailed" />.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<byte[]> OpenDirectoryAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.OpenDirectory, fields => fields.WriteString(path), cancellationToken).ConfigureAwait(false);
        return await ReadHandleAsync(id, SshTransferException.SftpOpenDirectoryFailed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_READDIR</c> for <paramref name="handle" /> and reads the names of
    /// the answer, with each one's attributes read as far as its file type.
    /// </summary>
    /// <param name="handle">The open directory's handle.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The names; empty at the end of the directory, and, as measured, for an answer of no names.</returns>
    /// <exception cref="SshTransferException">Any status but <c>SSH_FX_EOF</c>: <see cref="SshTransferException.SftpReadDirectoryFailed" />.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(byte[] handle, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.ReadDirectory, fields => fields.WriteString(handle), cancellationToken).ConfigureAwait(false);
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        if (type == SftpPacketType.Name)
        {
            return ReadEntries(answer);
        }

        uint status = ReadStatus(type, answer);
        return status == SftpStatusCode.EndOfFile ? [] : throw SshTransferException.SftpReadDirectoryFailed(status);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_READLINK</c> for <paramref name="path" /> and reads the first name
    /// of the answer.
    /// </summary>
    /// <param name="path">The symbolic link's path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The link's target, as the server sent its bytes, or <see langword="null" /> when the
    /// server answered <c>SSH_FX_OK</c>, which names no target and, as measured, leaves
    /// curl printing the entry's own name.
    /// </returns>
    /// <exception cref="SshTransferException">A failed status or an answer of no names: <see cref="SshTransferException.SftpReadLinkFailed" />.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<byte[]?> ReadLinkAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.ReadLink, fields => fields.WriteString(path), cancellationToken).ConfigureAwait(false);
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        if (type == SftpPacketType.Name)
        {
            return answer.ReadUInt32() > 0 ? answer.ReadString().ToArray() : throw SshTransferException.SftpReadLinkFailed();
        }

        return ReadStatus(type, answer) == SftpStatusCode.Ok ? null : throw SshTransferException.SftpReadLinkFailed();
    }

    /// <summary>
    /// Sends <c>SSH_FXP_STAT</c> for <paramref name="path" /> and reads the size from the
    /// answer. A size of 0, as measured, counts as unknown.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The size, or <see langword="null" /> when the server gave none, gave 0, answered with a status or sent attributes cut short.</returns>
    /// <exception cref="InvalidDataException">The answer is malformed.</exception>
    internal async ValueTask<long?> StatSizeAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.Stat, fields => fields.WriteString(path), cancellationToken).ConfigureAwait(false);
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        return type == SftpPacketType.Attributes ? ReadSize(answer) : null;
    }

    /// <summary>
    /// Sends <c>SSH_FXP_READ</c> for <paramref name="length" /> bytes at
    /// <paramref name="offset" />, without waiting for the answer.
    /// </summary>
    /// <param name="handle">The open file's handle.</param>
    /// <param name="offset">Where the read starts.</param>
    /// <param name="length">How many bytes to ask for.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The request's number, for <see cref="ReadDataAsync" />.</returns>
    internal ValueTask<uint> SendReadAsync(byte[] handle, long offset, int length, CancellationToken cancellationToken) =>
        SendRequestAsync(
            SftpPacketType.Read,
            fields =>
            {
                fields.WriteString(handle);
                fields.WriteUInt32((uint)(offset >> 32));
                fields.WriteUInt32((uint)offset);
                fields.WriteUInt32((uint)length);
            },
            cancellationToken);

    /// <summary>
    /// Reads the answer to the read numbered <paramref name="id" />.
    /// </summary>
    /// <param name="id">The read's number.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes read; empty at the end of the file, or for an empty answer.</returns>
    /// <exception cref="SshTransferException">Exit 79, <c>Error in the SSH layer</c>, for any status but <c>SSH_FX_EOF</c>, as measured.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<ReadOnlyMemory<byte>> ReadDataAsync(uint id, CancellationToken cancellationToken)
    {
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        if (type == SftpPacketType.Data)
        {
            return answer.ReadString();
        }

        return ReadStatus(type, answer) == SftpStatusCode.EndOfFile ? ReadOnlyMemory<byte>.Empty : throw SshTransferException.SshLayerError();
    }

    /// <summary>
    /// Sends <c>SSH_FXP_CLOSE</c> for <paramref name="handle" /> and waits for its answer,
    /// whatever it is: curl only logs a failed close.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes once the answer arrives.</returns>
    internal async ValueTask CloseHandleAsync(byte[] handle, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.Close, fields => fields.WriteString(handle), cancellationToken).ConfigureAwait(false);
        await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the session as libssh2's shutdown does: closes the channel.
    /// </summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes once the channel is closed.</returns>
    internal ValueTask ShutdownAsync(CancellationToken cancellationToken) => channel.CloseAsync(cancellationToken);

    /// <summary>
    /// Closes <paramref name="handle" />, when there is one, then the channel, as curl ends
    /// every SFTP transfer (ADR-0220): a close that fails is only logged by curl, and a
    /// broken connection has already decided the transfer's outcome, so neither fails.
    /// </summary>
    /// <param name="handle">The open file's or directory's handle, or <see langword="null" /> when nothing was opened.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes once the channel is closed or the connection found broken.</returns>
    internal async ValueTask FinishIgnoringFailureAsync(byte[]? handle, CancellationToken cancellationToken)
    {
        try
        {
            if (handle is not null)
            {
                await CloseHandleAsync(handle, cancellationToken).ConfigureAwait(false);
            }

            await ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }

    // A refusal, a close, a disconnect, broken framing or a failed packet check all fail
    // the step with libssh2's description for it.
    private static async ValueTask RequireAsync(Func<ValueTask<bool>> step, string failure)
    {
        bool succeeded;
        try
        {
            succeeded = await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException or SshPacketAuthenticationException)
        {
            succeeded = false;
        }

        if (!succeeded)
        {
            throw SshTransferException.SftpInitializationFailed(failure);
        }
    }

    // libssh2 reads each extension's name, then its data, after the version.
    private static void CheckExtensions(byte[] version)
    {
        SshWireReader reader = new(version.AsMemory(5));
        while (reader.RemainingLength > 0)
        {
            ReadExtensionPart(reader, ExtensionNameTooShort);
            ReadExtensionPart(reader, ExtensionDataTooShort);
        }
    }

    private static void ReadExtensionPart(SshWireReader reader, string failure)
    {
        try
        {
            reader.ReadString();
        }
        catch (InvalidDataException)
        {
            throw SshTransferException.SftpInitializationFailed(failure);
        }
    }

    private static uint ReadStatus(byte type, SshWireReader answer) =>
        type == SftpPacketType.Status ? answer.ReadUInt32() : throw new InvalidDataException($"The SFTP server answered with packet type {type}.");

    private static List<SftpDirectoryEntry> ReadEntries(SshWireReader answer)
    {
        uint count = answer.ReadUInt32();
        List<SftpDirectoryEntry> entries = [];
        for (uint index = 0; index < count; index++)
        {
            byte[] fileName = answer.ReadString().ToArray();
            byte[] longName = answer.ReadString().ToArray();
            entries.Add(new SftpDirectoryEntry(fileName, longName, ReadIsSymbolicLink(answer)));
        }

        return entries;
    }

    // Reads one entry's attributes (draft-ietf-secsh-filexfer-02 section 5) to their end,
    // telling whether their permissions name a symbolic link.
    private static bool ReadIsSymbolicLink(SshWireReader attributes)
    {
        uint flags = attributes.ReadUInt32();
        SkipWhenFlagged(attributes, flags, AttributeSize, 8);
        SkipWhenFlagged(attributes, flags, AttributeUserAndGroup, 8);
        bool isSymbolicLink = (flags & AttributePermissions) != 0 && (attributes.ReadUInt32() & FileTypeMask) == SymbolicLinkType;
        SkipWhenFlagged(attributes, flags, AttributeAccessAndModifyTimes, 8);
        uint extendedCount = (flags & AttributeExtended) != 0 ? attributes.ReadUInt32() : 0;
        for (uint index = 0; index < extendedCount; index++)
        {
            attributes.ReadString();
            attributes.ReadString();
        }

        return isSymbolicLink;
    }

    private static void SkipWhenFlagged(SshWireReader attributes, uint flags, uint flag, int length)
    {
        if ((flags & flag) != 0)
        {
            attributes.ReadBytes(length);
        }
    }

    private static long? ReadSize(SshWireReader attributes)
    {
        try
        {
            bool hasSize = (attributes.ReadUInt32() & AttributeSize) != 0;
            long size = hasSize ? (long)(((ulong)attributes.ReadUInt32() << 32) | attributes.ReadUInt32()) : 0;
            return size > 0 ? size : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // The handle answering the open numbered id; an SSH_FX_OK status is waited past.
    private async ValueTask<byte[]> ReadHandleAsync(uint id, Func<uint, SshTransferException> failure, CancellationToken cancellationToken)
    {
        while (true)
        {
            (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
            if (type == SftpPacketType.Handle)
            {
                return answer.ReadString().ToArray();
            }

            uint status = ReadStatus(type, answer);
            if (status != SftpStatusCode.Ok)
            {
                throw failure(status);
            }
        }
    }

    private async ValueTask<byte[]> InitializeAsync(CancellationToken cancellationToken)
    {
        await SendPacketAsync([SftpPacketType.Init, 0, 0, 0, (byte)ProtocolVersion], cancellationToken).ConfigureAwait(false);
        while (true)
        {
            byte[] packet = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
            if (packet.Length >= 5 && packet[0] == SftpPacketType.Version)
            {
                return packet;
            }
        }
    }

    private async ValueTask<uint> SendRequestAsync(byte type, Action<SshWireWriter> writeFields, CancellationToken cancellationToken)
    {
        uint id = nextRequestId++;
        SshWireWriter request = new();
        request.WriteByte(type);
        request.WriteUInt32(id);
        writeFields(request);
        await SendPacketAsync(request.ToArray(), cancellationToken).ConfigureAwait(false);
        return id;
    }

    private async ValueTask SendPacketAsync(byte[] payload, CancellationToken cancellationToken)
    {
        SshWireWriter packet = new();
        packet.WriteString(payload);
        await channel.SendAsync(packet.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SshWireReader> ReadAnswerAsync(uint id, byte wantedType, CancellationToken cancellationToken)
    {
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        return type == wantedType ? answer : throw SshTransferException.SftpRequestFailed(ReadStatus(type, answer));
    }

    // The answer numbered id: its type and a reader after the number. Answers to other
    // requests, such as reads sent ahead that are no longer wanted, are skipped.
    private async ValueTask<(byte Type, SshWireReader Answer)> ReadAnswerAsync(uint id, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] packet = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
            if (packet.Length >= 5 && BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(1)) == id)
            {
                return (packet[0], new SshWireReader(packet.AsMemory(5)));
            }
        }
    }

    private async ValueTask<byte[]> ReadPacketAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBytes = await ReadExactlyAsync(4, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        return length > MaximumPacketLength
            ? throw new InvalidDataException($"The SFTP packet of {length} bytes is longer than {MaximumPacketLength}.")
            : await ReadExactlyAsync((int)length, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[count];
        for (int filled = 0; filled < count;)
        {
            int read = await channel.ReadAsync(bytes.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            filled += read > 0 ? read : throw new EndOfStreamException("The SFTP channel ended inside a packet.");
        }

        return bytes;
    }
}
