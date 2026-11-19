using System.Net;
using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>A SASL mechanism the client answers connection.start with, and its connection.start-ok response (ADR-1312).</summary>
/// <param name="Name">The mechanism name as the server lists it.</param>
/// <param name="Response">The connection.start-ok response bytes.</param>
internal sealed record Amqp0Mechanism(string Name, byte[] Response)
{
    internal const string Plain = "PLAIN";

    internal const string AmqPlain = "AMQPLAIN";

    internal const string Anonymous = "ANONYMOUS";

    /// <summary>
    /// Picks the first mechanism in the client's preference order that the server offers: PLAIN, then ANONYMOUS when no user name
    /// was given, then AMQPLAIN. Null when the server offers none of them, so no credentials are sent.
    /// </summary>
    internal static Amqp0Mechanism? Choose(string offered, NetworkCredential? credentials)
    {
        string[] names = offered.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool hasUser = !string.IsNullOrEmpty(credentials?.UserName);
        NetworkCredential login = hasUser ? credentials! : new NetworkCredential(Amqp0ClientFlow.DefaultCredential, Amqp0ClientFlow.DefaultCredential);
        return TryPlain(names, login) ?? TryAnonymous(names, hasUser) ?? TryAmqPlain(names, login);
    }

    private static Amqp0Mechanism? TryPlain(string[] names, NetworkCredential login) =>
        names.Contains(Plain) ? new(Plain, System.Text.Encoding.UTF8.GetBytes($"\0{login.UserName}\0{login.Password}")) : null;

    private static Amqp0Mechanism? TryAnonymous(string[] names, bool hasUser) =>
        names.Contains(Anonymous) && !hasUser ? new(Anonymous, []) : null;

    private static Amqp0Mechanism? TryAmqPlain(string[] names, NetworkCredential login) =>
        names.Contains(AmqPlain) ? new(AmqPlain, AmqPlainResponse(login)) : null;
    /// <summary>The AMQPLAIN response: a field table body (no length prefix) of LOGIN and PASSWORD long strings.</summary>
    private static byte[] AmqPlainResponse(NetworkCredential login) => Amqp0FieldTableCodec.Encode(new(
    [
        new("LOGIN", Amqp0FieldValue.LongString(login.UserName)),
        new("PASSWORD", Amqp0FieldValue.LongString(login.Password)),
    ]));
}
