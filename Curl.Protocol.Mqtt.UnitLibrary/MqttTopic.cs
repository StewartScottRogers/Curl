using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Reads the topic out of an <c>mqtt://</c> URL the way curl 8.21.0's
/// <c>mqtt_get_topic</c> does: everything after the path's leading slash, percent-decoded
/// to bytes, so <c>a%2Fb</c> is the topic <c>a/b</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Uri.AbsolutePath" /> keeps escapes as written and re-escapes any <c>%</c>
/// not followed by two hex digits as <c>%25</c>, so every <c>%</c> it returns starts a
/// valid escape, and a stray one such as <c>%zz</c> decodes back to itself, as curl leaves
/// it.
/// </para>
/// </remarks>
internal static class MqttTopic
{
    /// <summary>The most bytes a topic's two-byte length field can carry.</summary>
    private const int MaximumLength = 0xFFFF;

    /// <summary>
    /// Decodes the topic from <paramref name="url" />.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The topic's bytes, never empty.</returns>
    /// <exception cref="MqttTransferException">
    /// The path names no topic, or the topic is over 65535 bytes; both are exit 3.
    /// </exception>
    internal static byte[] Decode(Uri url)
    {
        string path = url.AbsolutePath;
        if (path.Length <= 1)
        {
            throw new MqttTransferException(CurlExitCode.UrlMalformat, MqttTransferMessages.NoTopic);
        }

        byte[] topic = PercentDecode(Encoding.UTF8.GetBytes(path[1..]));
        if (topic.Length > MaximumLength)
        {
            throw new MqttTransferException(CurlExitCode.UrlMalformat, MqttTransferMessages.TopicTooLong);
        }

        return topic;
    }

    private static byte[] PercentDecode(byte[] encoded)
    {
        List<byte> decoded = new(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'%')
            {
                decoded.Add(Convert.FromHexString(Encoding.ASCII.GetString(encoded, index + 1, 2))[0]);
                index += 2;
            }
            else
            {
                decoded.Add(encoded[index]);
            }
        }

        return [.. decoded];
    }
}
