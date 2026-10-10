using System.Globalization;
using System.Text;

namespace Curl.Conformance.SshServer;

/// <summary>
/// OpenSSH's <c>scp</c> as upstream's test <c>sshd</c> runs it on a session channel (BL-1917),
/// reading and writing the real files the case names under its log directory. As a source
/// (<c>scp -f</c>, or <c>-pf</c>) it waits for the client's zero byte, then sends
/// <c>T&lt;mtime&gt; 0 &lt;atime&gt; 0</c> (for <c>-p</c>), <c>C0644 &lt;size&gt; &lt;name&gt;</c>,
/// each acknowledged, and the file's bytes and a zero byte; a missing path is the error line
/// <c>\x01scp: &lt;path&gt;: No such file or directory</c>. As a sink (<c>scp -t</c>) it sends a zero
/// byte, then answers <c>T</c>, <c>D</c> and <c>E</c> lines with a zero byte (making and leaving
/// directories for <c>D</c> and <c>E</c>) and each <c>C</c> line by writing the file, into the
/// path itself or, when the path is a directory, into it under the line's name; a file whose
/// folder is missing is the same error line. The process exits 1 after any error, else 0.
/// </summary>
internal static class SshServerScpProcess
{
    // OpenSSH's scp sends the file's own mode; the stand-in's files carry no Unix mode on
    // Windows, so every file is sent as an ordinary rw-r--r-- file.
    private const string FileMode = "0644";

    private static readonly byte[] Acknowledgement = [0];

    /// <summary>
    /// Runs <paramref name="command"/> on <paramref name="channel"/> to its end, then closes the channel with its exit status.
    /// </summary>
    /// <param name="channel">The channel the <c>exec</c> request started.</param>
    /// <param name="command">The <c>scp</c> command.</param>
    /// <param name="cancellationToken">Cancels the process.</param>
    /// <returns>The exit status.</returns>
    internal static async ValueTask<uint> RunAsync(SshServerSessionChannel channel, SshServerScpCommand command, CancellationToken cancellationToken)
    {
        SshServerChannelInput input = new(channel);
        string path = LocalPath(command.Path);
        bool succeeded = command.IsSource
            ? await SendAsync(channel, input, path, command.Path, cancellationToken).ConfigureAwait(false)
            : await ReceiveAsync(channel, input, path, cancellationToken).ConfigureAwait(false);
        uint exitStatus = succeeded ? 0u : 1u;
        await channel.CloseAsync(exitStatus, cancellationToken).ConfigureAwait(false);
        return exitStatus;
    }

    /// <summary>
    /// Maps a path the client names to one this machine opens: a Windows drive path that the
    /// URL gave a leading slash, such as <c>/C:/log/file</c>, loses the slashes before its drive.
    /// </summary>
    /// <param name="path">The path from the command.</param>
    /// <returns>The path to open.</returns>
    internal static string LocalPath(string path)
    {
        string trimmed = path.TrimStart('/');
        return trimmed.Length >= 2 && trimmed[1] == ':' && char.IsAsciiLetter(trimmed[0]) ? trimmed : path;
    }

    private static async ValueTask<bool> SendAsync(SshServerSessionChannel channel, SshServerChannelInput input, string path, string name, CancellationToken cancellationToken)
    {
        if (await input.ReadByteAsync(cancellationToken).ConfigureAwait(false) != 0)
        {
            return true;
        }

        if (!File.Exists(path))
        {
            await SendErrorAsync(channel, $"{name}: {(Directory.Exists(path) ? "not a regular file" : "No such file or directory")}", cancellationToken).ConfigureAwait(false);
            return false;
        }

        long modified = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
        long accessed = new DateTimeOffset(File.GetLastAccessTimeUtc(path)).ToUnixTimeSeconds();
        await SendLineAsync(channel, string.Create(CultureInfo.InvariantCulture, $"T{modified} 0 {accessed} 0"), cancellationToken).ConfigureAwait(false);
        await input.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        byte[] contents = File.ReadAllBytes(path);
        await SendLineAsync(channel, string.Create(CultureInfo.InvariantCulture, $"C{FileMode} {contents.Length} {Path.GetFileName(path)}"), cancellationToken).ConfigureAwait(false);
        await input.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        await channel.WriteDataAsync((byte[])[.. contents, 0], cancellationToken).ConfigureAwait(false);
        await input.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> ReceiveAsync(SshServerSessionChannel channel, SshServerChannelInput input, string path, CancellationToken cancellationToken)
    {
        await channel.WriteDataAsync(Acknowledgement, cancellationToken).ConfigureAwait(false);
        Sink sink = new(channel, input, path, Directory.Exists(path) ? path : null);
        while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { Length: > 0 } line)
        {
            if (!await sink.AnswerAsync(Encoding.UTF8.GetString(line), cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }

        return sink.Succeeded;
    }

    private static ValueTask SendLineAsync(SshServerSessionChannel channel, string line, CancellationToken cancellationToken) =>
        channel.WriteDataAsync(Encoding.UTF8.GetBytes(line + "\n"), cancellationToken);

    // scp's run_err: a 1 byte, "scp: ", the message and a line feed.
    private static ValueTask SendErrorAsync(SshServerSessionChannel channel, string message, CancellationToken cancellationToken) =>
        channel.WriteDataAsync((byte[])[1, .. Encoding.UTF8.GetBytes($"scp: {message}\n")], cancellationToken);

    // The sink's state: the directory a C line's file goes into, or none to write the path itself.
    private sealed class Sink(SshServerSessionChannel channel, SshServerChannelInput input, string path, string? directory)
    {
        private string? directory = directory;

        internal bool Succeeded { get; private set; } = true;

        // Answers one control line; false once the process is to stop.
        internal async ValueTask<bool> AnswerAsync(string line, CancellationToken cancellationToken)
        {
            switch (line[0])
            {
                case 'T':
                    break;
                case 'D':
                    directory = Path.Combine(directory ?? path, Name(line));
                    Directory.CreateDirectory(directory);
                    break;
                case 'E':
                    directory = Path.GetDirectoryName(directory);
                    break;
                case 'C':
                    return await ReceiveFileAsync(line, cancellationToken).ConfigureAwait(false);
                default:
                    await FailAsync($"protocol error: unexpected <{line}>", cancellationToken).ConfigureAwait(false);
                    return false;
            }

            await channel.WriteDataAsync(Acknowledgement, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // C<mode> <size> <name>: acknowledged unless its folder is missing, then its bytes and
        // the source's zero byte, acknowledged once the file is written.
        private async ValueTask<bool> ReceiveFileAsync(string line, CancellationToken cancellationToken)
        {
            string target = directory is null ? path : Path.Combine(directory, Name(line));
            if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(target))))
            {
                await FailAsync($"{target}: No such file or directory", cancellationToken).ConfigureAwait(false);
                return true;
            }

            long size = long.Parse(line.Split(' ')[1], CultureInfo.InvariantCulture);
            await channel.WriteDataAsync(Acknowledgement, cancellationToken).ConfigureAwait(false);
            File.WriteAllBytes(target, await input.ReadBlockAsync(size, cancellationToken).ConfigureAwait(false));
            if (await input.ReadByteAsync(cancellationToken).ConfigureAwait(false) != 0)
            {
                return false;
            }

            await channel.WriteDataAsync(Acknowledgement, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async ValueTask FailAsync(string message, CancellationToken cancellationToken)
        {
            Succeeded = false;
            await SendErrorAsync(channel, message, cancellationToken).ConfigureAwait(false);
        }

        // The name after a C or D line's mode and size.
        private static string Name(string line) => line.Split(' ', 3)[2];
    }
}
