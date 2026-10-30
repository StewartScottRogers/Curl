using System.Globalization;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh;

/// <content>The SSH message numbers, the handshake and the key exchanges (BL-4366).</content>
internal sealed partial class SshDiagnosticLog
{
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

    private static string Describe(SshNegotiatedAlgorithms algorithms) =>
        $"kex {algorithms.KeyExchange}, host key {algorithms.ServerHostKey}, "
        + $"cipher {algorithms.CipherClientToServer}/{algorithms.CipherServerToClient}, "
        + $"MAC {algorithms.MacClientToServer ?? "implicit"}/{algorithms.MacServerToClient ?? "implicit"}, "
        + $"compression {algorithms.CompressionClientToServer}/{algorithms.CompressionServerToClient}";
}
