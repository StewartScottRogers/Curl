using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Sftp;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Writes the SCP and SFTP session steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Ssh" /> (ADR-0222, BL-925): the failure that ends a
/// session as <c>error</c>, with its libssh2 code when it has one; an authentication method
/// refused and a host key taken unchecked as <c>warning</c>; the server, the algorithms, the
/// host key, the method that authenticated and the transfer as <c>info</c>; and each SSH
/// message number, channel request and SFTP request and status as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. No method takes a password, a pass phrase or a
/// private key, and messages are logged by number alone, never by content (ADR-0222,
/// decision 7).
/// </remarks>
internal sealed class SshDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>Gets the log that writes nothing, for a transport built without one.</summary>
    internal static SshDiagnosticLog None { get; } = new(NoDiagnosticLog.Instance);

    /// <summary>Logs, at <c>verbose</c>, the number of an SSH message sent.</summary>
    /// <param name="messageNumber">The payload's first byte.</param>
    internal void MessageSent(byte messageNumber)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(CultureInfo.InvariantCulture, $"sent SSH message {messageNumber}"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, the number of an SSH message received.</summary>
    /// <param name="messageNumber">The payload's first byte.</param>
    internal void MessageReceived(byte messageNumber)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(CultureInfo.InvariantCulture, $"received SSH message {messageNumber}"));
        }
    }

    /// <summary>Logs, at <c>info</c>, the server's identification string and the algorithms agreed.</summary>
    /// <param name="handshake">The identification exchange and algorithm negotiation's outcome.</param>
    internal void HandshakeNegotiated(SshNegotiatedHandshake handshake)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "server identification: " + handshake.ServerIdentification);
            Write(DiagnosticLogLevel.Info, "negotiated " + Describe(handshake.Algorithms));
        }
    }

    /// <summary>Logs, at <c>info</c>, the key exchange method run and how long it took.</summary>
    /// <param name="method">The key exchange method.</param>
    /// <param name="elapsed">How long the exchange took, <c>NEWKEYS</c> included.</param>
    internal void KeysExchanged(string method, TimeSpan elapsed)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(CultureInfo.InvariantCulture, $"key exchange {method} done in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>Logs, at <c>info</c>, a key re-exchange the server started and the algorithms it agreed.</summary>
    /// <param name="algorithms">The algorithms in force after it.</param>
    internal void KeysReExchanged(SshNegotiatedAlgorithms algorithms)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "key re-exchange: " + Describe(algorithms));
        }
    }

    /// <summary>Logs, at <c>info</c>, the host key's type and SHA-256 fingerprint.</summary>
    /// <param name="hostKey">The server's host key blob.</param>
    internal void HostKeyPresented(byte[] hostKey)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            string fingerprint = Convert.ToBase64String(SHA256.HashData(hostKey)).TrimEnd('=');
            Write(DiagnosticLogLevel.Info, $"host key {SshKeyBlobReader.ReadKeyTypeName(hostKey)} SHA256:{fingerprint}");
        }
    }

    /// <summary>
    /// Logs the host key check's verdict: <c>warning</c> when no known-hosts file was read
    /// and the key was taken unchecked, as <c>--insecure</c> does; <c>info</c> otherwise.
    /// </summary>
    /// <param name="options">The SSH options the check ran with.</param>
    /// <param name="knownHosts">The known-hosts file, or <see langword="null" /> under <c>-k</c>.</param>
    internal void HostKeyAccepted(SshOptions options, KnownHostsFile? knownHosts)
    {
        if (options.HostPublicKeySha256 is not null || options.HostPublicKeyMd5 is not null)
        {
            WriteIfEnabled(DiagnosticLogLevel.Info, "host key accepted: it matches the fingerprint given");
        }
        else if (knownHosts is null)
        {
            WriteIfEnabled(DiagnosticLogLevel.Warning, "host key accepted unchecked: no known_hosts file (--insecure)");
        }
        else
        {
            WriteIfEnabled(DiagnosticLogLevel.Info, "host key accepted: it matches known_hosts");
        }
    }

    /// <summary>Logs, at <c>info</c>, the authentication methods the server allows.</summary>
    /// <param name="methods">The server's method list, or <see langword="null" /> when <c>none</c> succeeded.</param>
    internal void AuthenticationMethodsListed(string? methods)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, methods is null ? "authenticated with none" : "server allows authentication: " + methods);
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an authentication method about to be tried.</summary>
    /// <param name="method">The method, such as <c>password</c>.</param>
    internal void AuthenticationTried(string method)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, "trying authentication with " + method);
        }
    }

    /// <summary>
    /// Logs an authentication method's outcome: <c>info</c> when it authenticated the user,
    /// <c>warning</c> when it did not and curl goes on.
    /// </summary>
    /// <param name="method">The method tried.</param>
    /// <param name="succeeded">Whether it authenticated the user.</param>
    internal void AuthenticationEnded(string method, bool succeeded)
    {
        DiagnosticLogLevel level = succeeded ? DiagnosticLogLevel.Info : DiagnosticLogLevel.Warning;
        if (log.IsEnabled(level))
        {
            Write(level, succeeded ? "authenticated with " + method : method + " did not authenticate the user");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, a session channel's opening and whether the server opened it.</summary>
    /// <param name="opened">Whether the server opened it.</param>
    internal void ChannelOpened(bool opened) =>
        WriteIfEnabled(DiagnosticLogLevel.Verbose, opened ? "session channel opened" : "session channel refused");

    /// <summary>Logs, at <c>verbose</c>, a channel request for a subsystem or a command and the server's answer.</summary>
    /// <param name="requestType"><c>subsystem</c> or <c>exec</c>.</param>
    /// <param name="value">The subsystem's name or the command line.</param>
    /// <param name="succeeded">Whether the server started it.</param>
    internal void ChannelRequested(byte[] requestType, byte[] value, bool succeeded)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"channel request {Encoding.Latin1.GetString(requestType)} {Encoding.Latin1.GetString(value)}: {(succeeded ? "started" : "refused")}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an SFTP request sent.</summary>
    /// <param name="type">The request's <c>SSH_FXP_*</c> packet type.</param>
    /// <param name="id">The request's id.</param>
    internal void SftpRequestSent(byte type, uint id)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(CultureInfo.InvariantCulture, $"SFTP request {type} id {id}"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, the answer to an SFTP request: its type, and its status for <c>SSH_FXP_STATUS</c>.</summary>
    /// <param name="packet">The answer, from its type byte on.</param>
    internal void SftpAnswerRead(byte[] packet)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, DescribeSftpAnswer(packet));
        }
    }

    /// <summary>Logs, at <c>info</c>, that the transfer's bytes start to move.</summary>
    /// <param name="scheme"><c>scp</c> or <c>sftp</c>.</param>
    /// <param name="path">The URL's path.</param>
    /// <param name="isUpload">Whether the transfer sends a file.</param>
    internal void TransferStarted(string scheme, string path, bool isUpload)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"{scheme} {(isUpload ? "upload to" : "download of")} {path} started");
        }
    }

    /// <summary>
    /// Logs how the transfer ended: <c>info</c> with the bytes and milliseconds for a success,
    /// <c>error</c> with the <see cref="CurlExitCode" /> and message for a failure.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    internal void TransferEnded(TransferResult result, TimeSpan elapsed)
    {
        if (!result.IsSuccess)
        {
            if (log.IsEnabled(DiagnosticLogLevel.Error))
            {
                Write(DiagnosticLogLevel.Error, FailureText(result.ExitCode, result.ErrorMessage));
            }
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"transfer finished: {result.BytesTransferred} bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>
    /// Logs, at <c>error</c>, a failure that ends the session with its <see cref="CurlExitCode" />
    /// and, when it came from libssh2's session startup, the libssh2 code it maps from.
    /// </summary>
    /// <param name="exception">The failure.</param>
    internal void Failed(SshTransferException exception)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            string text = FailureText(exception.ExitCode, exception.Message);
            Write(DiagnosticLogLevel.Error, exception.Libssh2Code is { } code
                ? string.Create(CultureInfo.InvariantCulture, $"{text} (libssh2 {code})")
                : text);
        }
    }

    private static string Describe(SshNegotiatedAlgorithms algorithms) =>
        $"kex {algorithms.KeyExchange}, host key {algorithms.ServerHostKey}, "
        + $"cipher {algorithms.CipherClientToServer}/{algorithms.CipherServerToClient}, "
        + $"MAC {algorithms.MacClientToServer ?? "implicit"}/{algorithms.MacServerToClient ?? "implicit"}, "
        + $"compression {algorithms.CompressionClientToServer}/{algorithms.CompressionServerToClient}";

    private static string DescribeSftpAnswer(byte[] packet) =>
        packet[0] == SftpPacketType.Status && packet.Length >= 9
            ? string.Create(CultureInfo.InvariantCulture, $"SFTP status {BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(5))}")
            : string.Create(CultureInfo.InvariantCulture, $"SFTP answer {packet[0]}");

    private static string FailureText(CurlExitCode exitCode, string? message) =>
        string.Create(CultureInfo.InvariantCulture, $"failed with {exitCode} ({(int)exitCode}): {message}");

    private void WriteIfEnabled(DiagnosticLogLevel level, string message)
    {
        if (log.IsEnabled(level))
        {
            Write(level, message);
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Ssh, message);
}
