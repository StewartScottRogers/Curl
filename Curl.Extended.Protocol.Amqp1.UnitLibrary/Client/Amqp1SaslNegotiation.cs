using System.Net;
using Curl.Extended.Protocol.Amqp1.Frames;
using Curl.Extended.Protocol.Amqp1.Types;

namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>
/// The SASL layer of Part 5 section 5.3: the <c>AMQP 3 1.0.0</c> headers, then sasl-init with PLAIN when the
/// transfer has credentials and ANONYMOUS otherwise, once the server's sasl-mechanisms offers it, then sasl-outcome,
/// which must be ok.
/// </summary>
internal static class Amqp1SaslNegotiation
{
    private const byte OkCode = 0;

    /// <summary>Authenticates over <paramref name="channel"/> as <paramref name="credentials"/>, or anonymously when null.</summary>
    internal static async ValueTask NegotiateAsync(Amqp1FrameChannel channel, NetworkCredential? credentials, string hostname)
    {
        await channel.SendHeaderAsync(Amqp1ProtocolHeader.SaslProtocolId).ConfigureAwait(false);
        await channel.ReceiveHeaderAsync(Amqp1ProtocolHeader.SaslProtocolId).ConfigureAwait(false);
        var mechanisms = await ReceiveAsync(channel, Amqp1CompositeType.SaslMechanisms).ConfigureAwait(false);
        var (mechanism, initialResponse) = credentials is null
            ? (Amqp1SaslInitialResponse.Anonymous, Amqp1SaslInitialResponse.ForAnonymous(""))
            : (Amqp1SaslInitialResponse.Plain, Amqp1SaslInitialResponse.ForPlain("", credentials.UserName, credentials.Password));
        if (!Offers(mechanisms["sasl-server-mechanisms"], mechanism))
        {
            throw Amqp1ClientMessages.MechanismNotOffered(mechanism);
        }

        await channel.SendAsync(Amqp1Frame.ForSasl(Amqp1Composite.Create(Amqp1CompositeType.SaslInit,
            ("mechanism", mechanism), ("initial-response", initialResponse), ("hostname", hostname)))).ConfigureAwait(false);
        var outcome = await ReceiveAsync(channel, Amqp1CompositeType.SaslOutcome).ConfigureAwait(false);
        if (!OkCode.Equals(outcome["code"]))
        {
            throw Amqp1ClientMessages.SaslFailed(outcome["code"]);
        }
    }

    private static bool Offers(object? serverMechanisms, Amqp1Symbol mechanism) => serverMechanisms switch
    {
        Amqp1Symbol single => single == mechanism,
        Amqp1Array array => array.Elements.Contains(mechanism),
        _ => false,
    };

    private static async ValueTask<Amqp1Composite> ReceiveAsync(Amqp1FrameChannel channel, Amqp1CompositeType expected)
    {
        var (body, _) = await channel.ReceiveAsync().ConfigureAwait(false);
        return body.Type == expected ? body : throw Amqp1ClientMessages.Unexpected(body.Type, expected);
    }
}
