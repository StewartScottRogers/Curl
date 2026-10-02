using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Runs an SFTP transfer's <c>-Q</c>/<c>--quote</c> commands as curl 8.21.0 does through
/// libssh2 1.11.1 (ADR-0247): those with no prefix after <c>REALPATH .</c> and before the
/// transfer's first request, those with a <c>-</c> once a successful transfer's handle is
/// closed and before the channel is, and those with a <c>+</c> never, as curl's SFTP has no
/// use for them. The first command that fails ends the transfer with exit 21 and curl's
/// message; a <c>*</c> passes over a failure the server reports. <c>pwd</c> and
/// <c>statvfs</c> write their answers to the header output, <c>-D</c>, as curl writes them.
/// Measured 2026-09-29 (BL-572).
/// </summary>
internal sealed class SftpQuoteCommands
{
    // What curl prints for pwd after the transfer, when it has freed the path.
    private static readonly byte[] NoPath = "(nil)"u8.ToArray();

    // The commands that send their paths in a request answered by a status alone, with
    // the name curl's failure message gives each.
    private static readonly FrozenDictionary<SftpQuoteOperation, (byte Type, string Name)> PathRequests =
        new Dictionary<SftpQuoteOperation, (byte Type, string Name)>
        {
            [SftpQuoteOperation.SymbolicLink] = (SftpPacketType.SymbolicLink, "symlink"),
            [SftpQuoteOperation.Rename] = (SftpPacketType.Rename, "rename"),
            [SftpQuoteOperation.RemoveDirectory] = (SftpPacketType.RemoveDirectory, "rmdir"),
            [SftpQuoteOperation.Remove] = (SftpPacketType.Remove, "rm"),
        }.ToFrozenDictionary();

    private readonly Stream? headerOutput;

    private readonly bool cLongIs32Bits;

    private readonly ITransferEvents events;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpQuoteCommands" /> class.
    /// </summary>
    /// <param name="beforeTransfer">The commands to run before the transfer, each with any <c>*</c>.</param>
    /// <param name="afterTransfer">The commands to run after a successful transfer, each less its <c>-</c> and with any <c>*</c>.</param>
    /// <param name="headerOutput">Where <c>pwd</c> and <c>statvfs</c> write, or <see langword="null" /> to write nowhere.</param>
    /// <param name="cLongIs32Bits">
    /// Whether the platform's C <c>long</c> is 32 bits, as on Windows. curl reads a
    /// <c>chown</c> or <c>chgrp</c> number up to <c>ULONG_MAX</c>, which on a 64-bit
    /// <c>long</c> overflows its own parser so that no number is ever read, and refuses a
    /// date past 32 bits only when <c>long</c> cannot hold one, as measured on each platform.
    /// </param>
    /// <param name="events">Where the <c>SSH: sending quote commands</c> line is reported.</param>
    internal SftpQuoteCommands(IReadOnlyList<string> beforeTransfer, IReadOnlyList<string> afterTransfer, Stream? headerOutput, bool cLongIs32Bits, ITransferEvents events)
    {
        this.events = events;
        BeforeTransfer = beforeTransfer;
        AfterTransfer = afterTransfer;
        this.headerOutput = headerOutput;
        this.cLongIs32Bits = cLongIs32Bits;
    }

    /// <summary>Gets no commands at all.</summary>
    internal static SftpQuoteCommands None { get; } = new([], [], null, cLongIs32Bits: true, NoTransferEvents.Instance);

    /// <summary>Gets the commands to run before the transfer, each with any <c>*</c>.</summary>
    internal IReadOnlyList<string> BeforeTransfer { get; }

    /// <summary>Gets the commands to run after a successful transfer, each less its <c>-</c> and with any <c>*</c>.</summary>
    internal IReadOnlyList<string> AfterTransfer { get; }

    /// <summary>
    /// Sorts the transfer's <c>-Q</c> values as the curl tool does: a first <c>-</c> runs the
    /// rest after the transfer, a first <c>+</c> is dropped, and any other runs before it.
    /// </summary>
    /// <param name="context">The transfer's context, with its <see cref="ITransferContext.QuoteCommands" />, <see cref="ITransferContext.HeaderOutput" /> and <see cref="ITransferContext.Events" />.</param>
    /// <param name="cLongIs32Bits">Whether the platform's C <c>long</c> is 32 bits, as on Windows.</param>
    /// <returns>The commands.</returns>
    internal static SftpQuoteCommands From(ITransferContext context, bool cLongIs32Bits)
    {
        List<string> before = [];
        List<string> after = [];
        foreach (string value in context.QuoteCommands)
        {
            if (value.StartsWith('-'))
            {
                after.Add(value[1..]);
            }
            else if (!value.StartsWith('+'))
            {
                before.Add(value);
            }
        }

        return new SftpQuoteCommands(before, after, context.HeaderOutput, cLongIs32Bits, context.Events);
    }

    /// <summary>
    /// Runs the commands that come before the transfer.
    /// </summary>
    /// <param name="session">The started session.</param>
    /// <param name="homeDirectory">The server's answer to <c>REALPATH .</c>.</param>
    /// <param name="workingPath">The transfer's resolved path, which <c>pwd</c> prints.</param>
    /// <param name="cancellationToken">Cancels the commands.</param>
    /// <returns>A task that completes once every command has run.</returns>
    /// <exception cref="SshTransferException">A command failed: exit 21 and curl's message, or exit 79 when the connection broke.</exception>
    internal ValueTask RunBeforeTransferAsync(SftpSession session, byte[] homeDirectory, byte[] workingPath, CancellationToken cancellationToken) =>
        RunAsync(BeforeTransfer, session, homeDirectory, workingPath, cancellationToken);

    /// <summary>
    /// Ends the transfer: closes <paramref name="handle" />, runs the commands that come
    /// after a successful transfer, and closes the channel.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="handle">The open handle, or <see langword="null" /> when nothing was opened.</param>
    /// <param name="homeDirectory">The server's answer to <c>REALPATH .</c>.</param>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="cancellationToken">Cancels the end.</param>
    /// <returns>
    /// <paramref name="result" />; or, when a command after it failed, the same bytes with
    /// that command's exit code and message.
    /// </returns>
    internal async ValueTask<TransferResult> FinishAsync(SftpSession session, byte[]? handle, byte[] homeDirectory, TransferResult result, CancellationToken cancellationToken)
    {
        TransferResult finished = result;
        await session.FinishIgnoringFailureAsync(
            handle,
            async () => finished = result.IsSuccess ? await RunAfterTransferAsync(session, homeDirectory, result, cancellationToken).ConfigureAwait(false) : result,
            cancellationToken).ConfigureAwait(false);
        return finished;
    }

    private static string Show(byte[] path) => Encoding.UTF8.GetString(path);

    // A failed status fails the command unless it starts with '*'.
    private static void Require(SftpQuoteCommand command, uint status, string failure)
    {
        if (status != SftpStatusCode.Ok && !command.IgnoresFailure)
        {
            throw SshTransferException.SftpQuoteFailed(failure + SftpStatusCode.DescriptionOf(status));
        }
    }

    // curl's str_num_base: at least one digit, and more until one that is not, failing
    // as soon as the number passes max.
    private static bool TryReadNumber(string text, uint numberBase, ulong max, out uint number)
    {
        ulong value = 0;
        int index = 0;
        for (; index < text.Length && text[index] >= '0' && text[index] < '0' + numberBase; index++)
        {
            value = (value * numberBase) + (uint)(text[index] - '0');
            if (value > max)
            {
                index = 0;
                break;
            }
        }

        number = (uint)value;
        return index > 0;
    }

    private async ValueTask<TransferResult> RunAfterTransferAsync(SftpSession session, byte[] homeDirectory, TransferResult result, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(AfterTransfer, session, homeDirectory, NoPath, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (SshTransferException failure)
        {
            return result with { ExitCode = failure.ExitCode, ErrorMessage = failure.Message };
        }
    }

    // curl announces a list before its first command, and an empty list not at all.
    private async ValueTask RunAsync(IReadOnlyList<string> commands, SftpSession session, byte[] homeDirectory, byte[] workingPath, CancellationToken cancellationToken)
    {
        if (commands.Count > 0)
        {
            events.ReportInfo(SshInfoLines.SendingQuoteCommands);
        }

        foreach (string value in commands)
        {
            SftpQuoteCommand command = SftpQuoteCommand.Parse(value, homeDirectory);
            await SshConnectionFailure.ReportAsSshLayerErrorAsync(async () =>
            {
                await RunAsync(command, session, workingPath, cancellationToken).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
        }
    }

    private ValueTask RunAsync(SftpQuoteCommand command, SftpSession session, byte[] workingPath, CancellationToken cancellationToken) =>
        command.Operation switch
        {
            SftpQuoteOperation.PrintWorkingDirectory => WriteHeaderAsync([.. "257 \""u8, .. workingPath, .. "\" is current directory.\n"u8], cancellationToken),
            SftpQuoteOperation.MakeDirectory => RequireAsync(command, session.MakeDirectoryAsync(command.FirstPath, cancellationToken), $"mkdir \"{Show(command.FirstPath)}\" failed: "),
            SftpQuoteOperation.StatFileSystem => StatFileSystemAsync(command, session, cancellationToken),
            _ when PathRequests.TryGetValue(command.Operation, out (byte Type, string Name) request) => RequirePathsAsync(command, session, request, cancellationToken),
            _ => ChangeAttributesAsync(command, session, cancellationToken),
        };

    // The request carries the command's paths, and its failure names them: both for a
    // symbolic link and a rename, the first alone otherwise.
    private static ValueTask RequirePathsAsync(SftpQuoteCommand command, SftpSession session, (byte Type, string Name) request, CancellationToken cancellationToken)
    {
        bool twoPaths = command.SecondPath.Length > 0;
        string failure = twoPaths
            ? $"{request.Name} \"{Show(command.FirstPath)}\" to \"{Show(command.SecondPath)}\" failed: "
            : $"{request.Name} \"{Show(command.FirstPath)}\" failed: ";
        return RequireAsync(
            command,
            session.RequestPathsAsync(request.Type, twoPaths ? [command.FirstPath, command.SecondPath] : [command.FirstPath], cancellationToken),
            failure);
    }

    private static async ValueTask RequireAsync(SftpQuoteCommand command, ValueTask<uint> request, string failure) =>
        Require(command, await request.ConfigureAwait(false), failure);

    private async ValueTask WriteHeaderAsync(byte[] lines, CancellationToken cancellationToken)
    {
        if (headerOutput is not null)
        {
            await headerOutput.WriteAsync(lines, cancellationToken).ConfigureAwait(false);
        }
    }

    // Measured: libssh2 fails a status answer even when it is OK, and a '*' then writes nothing.
    private async ValueTask StatFileSystemAsync(SftpQuoteCommand command, SftpSession session, CancellationToken cancellationToken)
    {
        (ulong[]? fields, uint status) = await session.StatFileSystemAsync(command.FirstPath, cancellationToken).ConfigureAwait(false);
        if (fields is null)
        {
            if (!command.IgnoresFailure)
            {
                throw SshTransferException.SftpQuoteFailed($"statvfs \"{Show(command.FirstPath)}\" failed: {SftpStatusCode.DescriptionOf(status)}");
            }

            return;
        }

        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"statvfs:\nf_bsize: {fields[0]}\nf_frsize: {fields[1]}\nf_blocks: {fields[2]}\nf_bfree: {fields[3]}\nf_bavail: {fields[4]}\nf_files: {fields[5]}\nf_ffree: {fields[6]}\nf_favail: {fields[7]}\nf_fsid: {fields[8]}\nf_flag: {fields[9]}\nf_namemax: {fields[10]}\n");
        await WriteHeaderAsync(Encoding.ASCII.GetBytes(text), cancellationToken).ConfigureAwait(false);
    }

    // Measured: chmod sets the permissions alone; every other attribute command first
    // reads the path's attributes and changes one field of them.
    private async ValueTask ChangeAttributesAsync(SftpQuoteCommand command, SftpSession session, CancellationToken cancellationToken)
    {
        SftpAttributes current = default;
        if (command.Operation != SftpQuoteOperation.ChangeMode)
        {
            (current, uint status) = await session.StatAsync(command.SecondPath, cancellationToken).ConfigureAwait(false);
            Require(command, status, "Attempt to get SFTP stats failed: ");
        }

        SftpAttributes changed = Change(command, current);
        uint setStatus = await session.SetStatAsync(command.SecondPath, changed, cancellationToken).ConfigureAwait(false);
        Require(command, setStatus, $"Attempt to set SFTP stats for \"{Show(command.SecondPath)}\" failed: ");
    }

    private SftpAttributes Change(SftpQuoteCommand command, SftpAttributes current)
    {
        string value = Show(command.FirstPath);
        return command.Operation switch
        {
            SftpQuoteOperation.ChangeMode => TryReadNumber(value, 8, 0xFFF, out uint mode)
                ? current with { Flags = SftpAttributes.PermissionsFlag, Permissions = mode }
                : throw SshTransferException.SftpQuoteFailed("Syntax error: chmod permissions not a number"),
            SftpQuoteOperation.ChangeGroup => TryReadId(value, out uint groupId)
                ? current with { Flags = SftpAttributes.UserAndGroupFlag, GroupId = groupId }
                : Unchanged(command, current, "Syntax error: chgrp gid not a number"),
            SftpQuoteOperation.ChangeOwner => TryReadId(value, out uint userId)
                ? current with { Flags = SftpAttributes.UserAndGroupFlag, UserId = userId }
                : Unchanged(command, current, "Syntax error: chown uid not a number"),
            _ => ChangeTime(command, current, value),
        };
    }

    // curl reads an owner or group number up to ULONG_MAX, which a 64-bit long makes -1.
    private bool TryReadId(string value, out uint id)
    {
        id = 0;
        return cLongIs32Bits && TryReadNumber(value, 10, uint.MaxValue, out id);
    }

    // Measured: a '*' sends the attributes STAT answered, as they are.
    private static SftpAttributes Unchanged(SftpQuoteCommand command, SftpAttributes current, string failure) =>
        command.IgnoresFailure ? current : throw SshTransferException.SftpQuoteFailed(failure);

    // Measured: libssh2 sends the low 32 bits of the date; a 32-bit long refuses one past them.
    private SftpAttributes ChangeTime(SftpQuoteCommand command, SftpAttributes current, string value)
    {
        string name = command.Text[..5];
        long seconds = CurlDateParser.TryParse(value, out long parsed)
            ? parsed
            : throw SshTransferException.SftpQuoteFailed($"incorrect date format for {name}");
        uint time = cLongIs32Bits && seconds > uint.MaxValue
            ? throw SshTransferException.SftpQuoteFailed("date overflow")
            : unchecked((uint)seconds);
        return command.Operation == SftpQuoteOperation.SetAccessTime
            ? current with { Flags = SftpAttributes.AccessAndModifyTimesFlag, AccessTime = time }
            : current with { Flags = SftpAttributes.AccessAndModifyTimesFlag, ModifyTime = time };
    }
}
