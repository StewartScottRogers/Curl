using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Net.Sockets;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The production <see cref="ISshAgentConnector" />, which finds the agent where libssh2
/// 1.11.1 looks (ADR-0270): on Windows the named pipe <c>SSH_AUTH_SOCK</c> names, or
/// Win32-OpenSSH's <c>\\.\pipe\openssh-ssh-agent</c> when it is unset; elsewhere the Unix
/// domain socket <c>SSH_AUTH_SOCK</c> names, and no agent when it is unset.
/// </summary>
/// <param name="readEnvironmentVariable">Reads <c>SSH_AUTH_SOCK</c>.</param>
/// <param name="isWindows">Whether the agent is looked for as the Windows build looks for it.</param>
/// <param name="windowsDefaultPipe">The pipe the Windows build opens when <c>SSH_AUTH_SOCK</c> is unset.</param>
internal sealed class SystemSshAgentConnector(Func<string, string?> readEnvironmentVariable, bool isWindows, string windowsDefaultPipe = SystemSshAgentConnector.WindowsOpenSshAgentPipe)
    : ISshAgentConnector
{
    /// <summary>The variable that names the agent's socket or pipe.</summary>
    internal const string AuthSocketVariable = "SSH_AUTH_SOCK";

    /// <summary>Where the Windows build looks when <c>SSH_AUTH_SOCK</c> is unset: Win32-OpenSSH's agent.</summary>
    internal const string WindowsOpenSshAgentPipe = @"\\.\pipe\openssh-ssh-agent";

    private const string PipeFolder = "pipe";

    /// <inheritdoc />
    public ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken)
    {
        string? path = readEnvironmentVariable(AuthSocketVariable);
        if (isWindows)
        {
            return PipeNameOf(path ?? windowsDefaultPipe) is { } pipe ? ConnectPipeAsync(pipe.Server, pipe.Name, cancellationToken) : ValueTask.FromResult<Stream?>(null);
        }

        return string.IsNullOrEmpty(path) ? ValueTask.FromResult<Stream?>(null) : ConnectUnixSocketAsync(path, cancellationToken);
    }

    /// <summary>
    /// Splits a Windows pipe path, <c>\\server\pipe\name</c>, into its server and name.
    /// libssh2 hands the path to <c>CreateFileA</c> whatever it is; only a pipe is opened
    /// here, so any other path is no agent (ADR-0270).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The server and pipe name, or <see langword="null" /> for a path that names no pipe.</returns>
    internal static (string Server, string Name)? PipeNameOf(string path)
    {
        string[] parts = path.Split('\\');
        return parts.Length == 5 && IsPipePath(parts) ? (parts[2], parts[4]) : null;
    }

    // Two empty parts before the server, then the server, the pipe folder and the name.
    private static bool IsPipePath(string[] parts) =>
        parts[0].Length + parts[1].Length == 0 && parts[2].Length > 0 && parts[3].Equals(PipeFolder, StringComparison.OrdinalIgnoreCase) && parts[4].Length > 0;

    // Excluded per ADR-0083: every line needs a live pipe, so the Integration run measures it.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin named pipe adapter, measured by the Integration run.")]
    private static async ValueTask<Stream?> ConnectPipeAsync(string server, string name, CancellationToken cancellationToken)
    {
        NamedPipeClientStream pipe = new(server, name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            // No wait for a pipe that is not there: libssh2's CreateFileA fails at once.
            await pipe.ConnectAsync(0, cancellationToken).ConfigureAwait(false);
            return pipe;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    // Excluded per ADR-0083: every line needs a live socket, so the Integration run measures it.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin Unix socket adapter, measured by the Integration run.")]
    private static async ValueTask<Stream?> ConnectUnixSocketAsync(string path, CancellationToken cancellationToken)
    {
        Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            socket.Dispose();
            return null;
        }
    }
}
