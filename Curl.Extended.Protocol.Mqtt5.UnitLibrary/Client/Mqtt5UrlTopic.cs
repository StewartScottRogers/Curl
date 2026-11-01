using Curl.Extended.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5.Client;

/// <summary>The topic an <c>mqtt5://</c> URL names: its path after the first '/', URL-decoded, as curl's <c>mqtt://</c> takes it.</summary>
internal static class Mqtt5UrlTopic
{
    /// <summary>
    /// The URL's topic. A URL with no path after '/' ends the transfer with <see cref="CurlExitCode.UrlMalformat"/>, as
    /// curl's <c>mqtt://</c> does.
    /// </summary>
    internal static string Decode(CurlUrl url)
    {
        string path = url.AbsolutePath;
        if (path.Length <= 1)
        {
            throw new Mqtt5ClientException(CurlExitCode.UrlMalformat, Mqtt5ClientMessages.NoTopic);
        }

        return Uri.UnescapeDataString(path[1..]);
    }
}
