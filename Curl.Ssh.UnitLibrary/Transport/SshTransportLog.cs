using System.Globalization;
using System.Security.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Ssh.HostKeys;
using Curl.Ssh.Negotiation;

namespace Curl.Ssh.Transport;

/// <summary>
/// Writes the SSH transport layer's steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Ssh" /> (ADR-0222, BL-925): the server, the algorithms,
/// the key exchange and the host key as <c>info</c>, and each SSH message number as
/// <c>verbose</c>. The layers above the transport write their own steps to the same
/// <see cref="Log" />.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. Messages are logged by number alone, never by
/// content (ADR-0222, decision 7).
/// </remarks>
internal sealed class SshTransportLog(IDiagnosticLog log)
{
    /// <summary>Gets the log that writes nothing, for a transport built without one.</summary>
    internal static SshTransportLog None { get; } = new(NoDiagnosticLog.Instance);

    /// <summary>Gets the diagnostic log these lines go to.</summary>
    internal IDiagnosticLog Log => log;

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

    private static string Describe(SshNegotiatedAlgorithms algorithms) =>
        $"kex {algorithms.KeyExchange}, host key {algorithms.ServerHostKey}, "
        + $"cipher {algorithms.CipherClientToServer}/{algorithms.CipherServerToClient}, "
        + $"MAC {algorithms.MacClientToServer ?? "implicit"}/{algorithms.MacServerToClient ?? "implicit"}, "
        + $"compression {algorithms.CompressionClientToServer}/{algorithms.CompressionServerToClient}";

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Ssh, message);
}
