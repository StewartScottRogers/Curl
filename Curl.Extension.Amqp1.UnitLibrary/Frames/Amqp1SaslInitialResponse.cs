using System.Text;
using Curl.Extension.Amqp1.Types;

namespace Curl.Extension.Amqp1.Frames;

/// <summary>
/// The mechanism names and initial responses a sasl-init frame (Part 5 section 5.3.3.2) carries for the PLAIN
/// (RFC 4616) and ANONYMOUS (RFC 4505) mechanisms.
/// </summary>
internal static class Amqp1SaslInitialResponse
{
    /// <summary>The PLAIN mechanism name.</summary>
    public static Amqp1Symbol Plain { get; } = new("PLAIN");

    /// <summary>The ANONYMOUS mechanism name.</summary>
    public static Amqp1Symbol Anonymous { get; } = new("ANONYMOUS");

    /// <summary>
    /// The PLAIN message of RFC 4616 section 2: authzid, NUL, authcid, NUL, passwd, in UTF-8; raises
    /// <see cref="Amqp1MalformedEncodingException"/> when a part holds NUL, which the message cannot carry.
    /// </summary>
    public static byte[] ForPlain(string authorizationIdentity, string userName, string password)
    {
        if (authorizationIdentity.Contains('\0') || userName.Contains('\0') || password.Contains('\0'))
        {
            throw new Amqp1MalformedEncodingException("A SASL PLAIN identity or password cannot hold NUL.");
        }

        return Encoding.UTF8.GetBytes($"{authorizationIdentity}\0{userName}\0{password}");
    }

    /// <summary>The ANONYMOUS message of RFC 4505 section 2: the trace information, in UTF-8, possibly empty.</summary>
    public static byte[] ForAnonymous(string trace) => Encoding.UTF8.GetBytes(trace);
}
