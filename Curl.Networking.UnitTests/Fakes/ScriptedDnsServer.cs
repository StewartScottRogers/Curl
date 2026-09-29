using System.Net;
using System.Net.Sockets;

namespace Curl.Networking.Fakes;

/// <summary>
/// How one fake DNS server behind <see cref="ScriptedDnsSocketOpener" /> answers: each UDP query
/// through <see cref="AnswerOverUdp" /> and each TCP one through <see cref="AnswerOverTcp" />, where
/// a <see langword="null" /> reply is silence; or it cannot be reached at all.
/// </summary>
public sealed class ScriptedDnsServer
{
    /// <summary>Gets a server that never answers.</summary>
    public static ScriptedDnsServer Silent => new();

    /// <summary>Gets or sets what a UDP query gets back; <see langword="null" /> for silence.</summary>
    public Func<byte[], byte[]?> AnswerOverUdp { get; init; } = static _ => null;

    /// <summary>Gets or sets what a TCP query gets back; <see langword="null" /> closes the connection unanswered.</summary>
    public Func<byte[], byte[]?> AnswerOverTcp { get; init; } = static _ => null;

    /// <summary>Gets or sets the exception opening a UDP socket to this server throws.</summary>
    public SocketException? OpenFailure { get; init; }

    /// <summary>Gets or sets the exception receiving from this server throws, as an ICMP port unreachable makes it.</summary>
    public SocketException? ReceiveFailure { get; init; }

    /// <summary>Gets or sets a datagram, made from the query, that arrives ahead of the reply and which the resolver must ignore.</summary>
    public Func<byte[], (EndPoint Source, byte[] Datagram)>? StrayDatagram { get; init; }

    /// <summary>Gets or sets the end point replies arrive from; <see langword="null" /> for the server's own.</summary>
    public EndPoint? ReplySource { get; init; }

    /// <summary>A server that answers every query over UDP with <paramref name="addresses" /> of the family asked for.</summary>
    /// <param name="addresses">The addresses.</param>
    /// <returns>The server.</returns>
    public static ScriptedDnsServer Answering(params IPAddress[] addresses) =>
        new() { AnswerOverUdp = query => DnsTestReplies.Answer(query, addresses) };

    /// <summary>A server that answers every query over UDP with <paramref name="responseCode" /> and no records.</summary>
    /// <param name="responseCode">The RCODE.</param>
    /// <returns>The server.</returns>
    public static ScriptedDnsServer AnsweringCode(int responseCode) =>
        new() { AnswerOverUdp = query => DnsTestReplies.Answer(query, [], responseCode) };
}
