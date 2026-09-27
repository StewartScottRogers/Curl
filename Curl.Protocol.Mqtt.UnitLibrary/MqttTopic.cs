using System.Globalization;
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
/// <see cref="CurlUrl.AbsolutePath" /> keeps the path as written, so a <c>%</c> not
/// followed by two hexadecimal digits, such as <c>%zz</c>, is copied as it is, as curl
/// leaves it.
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
    internal static byte[] Decode(CurlUrl url)
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
        int index = 0;
        while (index < encoded.Length)
        {
            decoded.Add(DecodeAt(encoded, ref index));
        }

        return [.. decoded];
    }

    private static byte DecodeAt(byte[] encoded, ref int index)
    {
        if (encoded[index] == '%'
            && index + 2 < encoded.Length
            && byte.TryParse(encoded.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte escaped))
        {
            index += 3;
            return escaped;
        }

        return encoded[index++];
    }
}
