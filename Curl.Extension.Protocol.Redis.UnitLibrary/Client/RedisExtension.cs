using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Redis.Client;

/// <summary>The <c>redis://</c> and <c>rediss://</c> extension's description and handler factory (ADR-1079).</summary>
public static class RedisExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "redis",
        "Sends one Redis command over RESP3 and writes its reply as redis-cli --raw does: the URL path is /[db]/command/argument/..., one argument per segment, -d appends the data as the last argument; --user or the URL's user information authenticate with HELLO 3 AUTH, rediss:// adds TLS.",
        ["redis", "rediss"],
        [],
        new Uri("https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new RedisProtocolHandler(services.Connector);
    }
}
