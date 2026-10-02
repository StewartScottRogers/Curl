using System.Buffers.Binary;
using Curl.Protocol.Ssh.Connection;
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

    // The attribute flags, and the file-type bits libssh2 sends with an open or a mkdir
    // and reads from a directory's entries.
    private const uint AttributeSize = 0x00000001;

    private const uint AttributeUserAndGroup = 0x00000002;

    private const uint AttributePermissions = 0x00000004;

    private const uint AttributeAccessAndModifyTimes = 0x00000008;

    private const uint AttributeExtended = 0x80000000;

    private const uint FileTypeMask = 0xF000;

    private const uint DirectoryType = 0x4000;

    private const uint RegularFileType = 0x8000;

    // curl's CURLOPT_NEW_DIRECTORY_PERMS default, which the curl tool never changes.
    private const uint NewDirectoryPermissions = 0x1ED;

    private const uint SymbolicLinkType = 0xA000;

    // statvfs@openssh.com answers eleven 64-bit fields; libssh2 keeps only ST_RDONLY and
    // ST_NOSUID of f_flag, the tenth.
    private const int StatFileSystemFieldCount = 11;

    private const int StatFileSystemFlagField = 9;

    private const ulong StatFileSystemFlagsKept = 0x3;

    private static readonly byte[] StatFileSystemExtension = "statvfs@openssh.com"u8.ToArray();

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
        (byte[]? handle, uint status) = await OpenAsync(path, SftpOpenFlags.Read, createFileMode, cancellationToken).ConfigureAwait(false);
        return handle ?? throw SshTransferException.SftpOpenFailed(status);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_OPEN</c> with <paramref name="flags" /> and the permissions
    /// attribute libssh2 adds: a regular file with <paramref name="createFileMode" />. A
    /// status of <c>SSH_FX_OK</c> is not an answer: as measured, curl goes on waiting.
    /// </summary>
    /// <param name="path">The path to open.</param>
    /// <param name="flags">The <see cref="SftpOpenFlags" /> bits.</param>
    /// <param name="createFileMode">The permission bits, curl's <c>--create-file-mode</c>, 0644 unless given.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The file's handle, or <see langword="null" /> and the failed status the server answered with.</returns>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<(byte[]? Handle, uint Status)> OpenAsync(byte[] path, uint flags, UnixFileMode createFileMode, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(
            SftpPacketType.Open,
            fields =>
            {
                fields.WriteString(path);
                fields.WriteUInt32(flags);
                fields.WriteUInt32(AttributePermissions);
                fields.WriteUInt32(RegularFileType | (uint)createFileMode);
            },
            cancellationToken).ConfigureAwait(false);
        return await ReadHandleOrStatusAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_MKDIR</c> for <paramref name="path" /> with the permissions
    /// attribute libssh2 adds: a directory with mode 0755, curl's default, as measured.
    /// </summary>
    /// <param name="path">The directory to create.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The status the server answered with.</returns>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<uint> MakeDirectoryAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(
            SftpPacketType.MakeDirectory,
            fields =>
            {
                fields.WriteString(path);
                fields.WriteUInt32(AttributePermissions);
                fields.WriteUInt32(DirectoryType | NewDirectoryPermissions);
            },
            cancellationToken).ConfigureAwait(false);
        return await ReadStatusAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request that names paths and is answered by a status alone:
    /// <c>SSH_FXP_REMOVE</c>, <c>SSH_FXP_RMDIR</c>, <c>SSH_FXP_RENAME</c> or
    /// <c>SSH_FXP_SYMLINK</c>. libssh2 1.11.1 sends a version 3 <c>RENAME</c> with no flags,
    /// and a <c>SYMLINK</c> with its two paths in the order it is given them.
    /// </summary>
    /// <param name="type">The request's <see cref="SftpPacketType" />.</param>
    /// <param name="paths">The paths, in the order the request carries them.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The status the server answered with.</returns>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<uint> RequestPathsAsync(byte type, byte[][] paths, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(
            type,
            fields =>
            {
                foreach (byte[] path in paths)
                {
                    fields.WriteString(path);
                }
            },
            cancellationToken).ConfigureAwait(false);
        return await ReadStatusAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_STAT</c> for <paramref name="path" /> and reads the whole answer, as
    /// libssh2's <c>libssh2_sftp_stat_ex</c> does.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The attributes and <see cref="SftpStatusCode.Ok" />; or, for a status answer, empty
    /// attributes and that status, which libssh2 takes as success when it is
    /// <see cref="SftpStatusCode.Ok" />.
    /// </returns>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<(SftpAttributes Attributes, uint Status)> StatAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(SftpPacketType.Stat, fields => fields.WriteString(path), cancellationToken).ConfigureAwait(false);
        return await ReadAttributesOrStatusAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_SETSTAT</c> for <paramref name="path" /> with
    /// <paramref name="attributes" /> as libssh2 writes them. An attributes answer counts as
    /// success, as libssh2 takes it.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="attributes">The attributes to set.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The status the server answered with; <see cref="SftpStatusCode.Ok" /> for an attributes answer.</returns>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<uint> SetStatAsync(byte[] path, SftpAttributes attributes, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(
            SftpPacketType.SetStat,
            fields =>
            {
                fields.WriteString(path);
                attributes.WriteTo(fields);
            },
            cancellationToken).ConfigureAwait(false);
        return (await ReadAttributesOrStatusAsync(id, cancellationToken).ConfigureAwait(false)).Status;
    }

    /// <summary>
    /// Sends OpenSSH's <c>statvfs@openssh.com</c> extension for <paramref name="path" /> and
    /// reads its eleven 64-bit fields, keeping only the read-only and no-set-uid bits of
    /// <c>f_flag</c>, as libssh2's <c>libssh2_sftp_statvfs</c> does.
    /// </summary>
    /// <param name="path">A path on the file system.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The fields in OpenSSH's order - <c>f_bsize</c>, <c>f_frsize</c>, <c>f_blocks</c>,
    /// <c>f_bfree</c>, <c>f_bavail</c>, <c>f_files</c>, <c>f_ffree</c>, <c>f_favail</c>,
    /// <c>f_fsid</c>, <c>f_flag</c>, <c>f_namemax</c> - and <see cref="SftpStatusCode.Ok" />;
    /// or <see langword="null" /> and the status the server answered with, which libssh2
    /// fails on even when it is <see cref="SftpStatusCode.Ok" />.
    /// </returns>
    /// <exception cref="InvalidDataException">The answer is cut short or of another type.</exception>
    internal async ValueTask<(ulong[]? Fields, uint Status)> StatFileSystemAsync(byte[] path, CancellationToken cancellationToken)
    {
        uint id = await SendRequestAsync(
            SftpPacketType.Extended,
            fields =>
            {
                fields.WriteString(StatFileSystemExtension);
                fields.WriteString(path);
            },
            cancellationToken).ConfigureAwait(false);
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        if (type != SftpPacketType.ExtendedReply)
        {
            return (null, ReadStatus(type, answer));
        }

        ulong[] values = new ulong[StatFileSystemFieldCount];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = ((ulong)answer.ReadUInt32() << 32) | answer.ReadUInt32();
        }

        values[StatFileSystemFlagField] &= StatFileSystemFlagsKept;
        return (values, SftpStatusCode.Ok);
    }

    /// <summary>
    /// Sends <c>SSH_FXP_WRITE</c> of <paramref name="data" /> at <paramref name="offset" />,
    /// without waiting for the answer.
    /// </summary>
    /// <param name="handle">The open file's handle.</param>
    /// <param name="offset">Where the bytes go in the file.</param>
    /// <param name="data">The bytes.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The request's number, for <see cref="ReadStatusAsync" />.</returns>
    internal ValueTask<uint> SendWriteAsync(byte[] handle, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
        SendRequestAsync(
            SftpPacketType.Write,
            fields =>
            {
                fields.WriteString(handle);
                fields.WriteUInt32((uint)(offset >> 32));
                fields.WriteUInt32((uint)offset);
                fields.WriteString(data.Span);
            },
            cancellationToken);

    /// <summary>
    /// Reads the status that answers the request numbered <paramref name="id" />.
    /// </summary>
    /// <param name="id">The request's number.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The <c>SSH_FX_*</c> code.</returns>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<uint> ReadStatusAsync(uint id, CancellationToken cancellationToken)
    {
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        return ReadStatus(type, answer);
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
    /// Closes <paramref name="handle" />, when there is one, runs
    /// <paramref name="afterClose" />, then closes the channel, as curl ends every SFTP
    /// transfer (ADR-0220), running its <c>-Q -</c> commands between the two closes
    /// (ADR-0247): a close that fails is only logged by curl, and a broken connection has
    /// already decided the transfer's outcome, so neither fails, and a broken connection
    /// before <paramref name="afterClose" /> skips it.
    /// </summary>
    /// <param name="handle">The open file's or directory's handle, or <see langword="null" /> when nothing was opened.</param>
    /// <param name="afterClose">What to do once the handle is closed and before the channel is.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes once the channel is closed or the connection found broken.</returns>
    internal async ValueTask FinishIgnoringFailureAsync(byte[]? handle, Func<ValueTask> afterClose, CancellationToken cancellationToken)
    {
        try
        {
            if (handle is not null)
            {
                await CloseHandleAsync(handle, cancellationToken).ConfigureAwait(false);
            }

            await afterClose().ConfigureAwait(false);
            await ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }

    /// <summary>
    /// Runs <paramref name="transfer" />, and when it fails - a failed <c>REALPATH</c>,
    /// <c>OPEN</c>, <c>OPENDIR</c> or <c>-Q</c> command - closes the channel with
    /// <c>EOF</c> and <c>CLOSE</c> before passing the failure on, as curl's disconnect
    /// shuts the SFTP session down before its <c>DISCONNECT</c>, measured 2026-09-30
    /// (BL-973, ADR-0220). A connection already broken is left as it is.
    /// </summary>
    /// <typeparam name="T">What the transfer returns.</typeparam>
    /// <param name="transfer">The transfer, from <c>REALPATH</c> on.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>What the transfer returned.</returns>
    /// <exception cref="SshTransferException">The transfer's failure, passed on once the channel is closed.</exception>
    internal async ValueTask<T> CloseChannelOnFailureAsync<T>(Func<ValueTask<T>> transfer, CancellationToken cancellationToken)
    {
        // The close is awaited after the catch, not inside it: an await inside a catch that
        // rethrows makes the compiler add a rethrow branch no exception can take.
        SshTransferException failure;
        try
        {
            return await transfer().ConfigureAwait(false);
        }
        catch (SshTransferException caught)
        {
            failure = caught;
        }

        await FinishIgnoringFailureAsync(null, () => ValueTask.CompletedTask, cancellationToken).ConfigureAwait(false);
        throw failure;
    }

    // A refusal, a close, a disconnect, a reset (BL-1046), broken framing or a failed
    // packet check all fail the step with libssh2's description for it, as measured.
    private static async ValueTask RequireAsync(Func<ValueTask<bool>> step, string failure)
    {
        bool succeeded;
        try
        {
            succeeded = await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
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

    // The attributes answering the request numbered id, or empty ones and the status.
    private async ValueTask<(SftpAttributes Attributes, uint Status)> ReadAttributesOrStatusAsync(uint id, CancellationToken cancellationToken)
    {
        (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
        return type == SftpPacketType.Attributes
            ? (SftpAttributes.Read(answer), SftpStatusCode.Ok)
            : (default, ReadStatus(type, answer));
    }

    // The handle answering the open numbered id; an SSH_FX_OK status is waited past.
    private async ValueTask<byte[]> ReadHandleAsync(uint id, Func<uint, SshTransferException> failure, CancellationToken cancellationToken)
    {
        (byte[]? handle, uint status) = await ReadHandleOrStatusAsync(id, cancellationToken).ConfigureAwait(false);
        return handle ?? throw failure(status);
    }

    // The handle answering the open numbered id, or the failed status; SSH_FX_OK is waited past.
    private async ValueTask<(byte[]? Handle, uint Status)> ReadHandleOrStatusAsync(uint id, CancellationToken cancellationToken)
    {
        while (true)
        {
            (byte type, SshWireReader answer) = await ReadAnswerAsync(id, cancellationToken).ConfigureAwait(false);
            if (type == SftpPacketType.Handle)
            {
                return (answer.ReadString().ToArray(), SftpStatusCode.Ok);
            }

            uint status = ReadStatus(type, answer);
            if (status != SftpStatusCode.Ok)
            {
                return (null, status);
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
        channel.DiagnosticLog.SftpRequestSent(type, id);
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
                channel.DiagnosticLog.SftpAnswerRead(packet);
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
