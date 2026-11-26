using Curl.Extension.Abstractions;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>
/// The virtual host and queue an <c>amqp://</c> URL names (ADR-1060): the path is <c>/virtual-host/queue</c>, each
/// segment URL-decoded, and an empty virtual host is <c>/</c> as RabbitMQ's URI specification has it.
/// </summary>
internal sealed record Amqp0UrlQueue(string VirtualHost, string Queue)
{
    private const string DefaultVirtualHost = "/";

    /// <summary>
    /// The URL's virtual host and queue. A path that is not two segments, an empty queue, or either decoding to bytes
    /// that are not valid UTF-8 (ADR-1418), ends the transfer with <see cref="CurlExitCode.UrlMalformat"/> before
    /// anything is connected.
    /// </summary>
    internal static Amqp0UrlQueue Decode(CurlUrl url)
    {
        string[] segments = url.AbsolutePath.Split('/');
        if (segments.Length != 3 || segments[2].Length == 0)
        {
            throw new Amqp0ClientException(CurlExitCode.UrlMalformat, Amqp0ClientMessages.NoQueue);
        }

        string virtualHost = DecodedText(segments[1], Amqp0ClientMessages.VirtualHostNotUtf8);
        return new Amqp0UrlQueue(virtualHost.Length == 0 ? DefaultVirtualHost : virtualHost, DecodedText(segments[2], Amqp0ClientMessages.QueueNotUtf8));
    }

    private static string DecodedText(string segment, string notUtf8Message) =>
        UrlPercentDecoding.Utf8TextOf(segment) ?? throw new Amqp0ClientException(CurlExitCode.UrlMalformat, notUtf8Message);
}
