using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Mqtt;

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
    /// The path names no topic, the decoded topic holds a control byte (0x00 to 0x1F, as
    /// curl's <c>Curl_urldecode</c> rejects with <c>REJECT_CTRL</c>; upstream test 2109), or
    /// the topic is over 65535 bytes; all three are exit 3.
    /// </exception>
    internal static byte[] Decode(CurlUrl url) => Decode(url, MqttCommandLineText.Platform);

    /// <summary>
    /// Decodes the topic from <paramref name="url" />, reading literal characters as
    /// <paramref name="commandLineText" />'s bytes.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="commandLineText">The encoding the platform's curl received the URL in.</param>
    /// <returns>The topic's bytes, never empty.</returns>
    /// <exception cref="MqttTransferException">The topic is missing, holds a control byte or is too long; exit 3.</exception>
    internal static byte[] Decode(CurlUrl url, MqttCommandLineText commandLineText)
    {
        string path = url.AbsolutePath;
        if (path.Length <= 1)
        {
            throw new MqttTransferException(CurlExitCode.UrlMalformat, MqttTransferMessages.NoTopic);
        }

        byte[] topic = PercentDecode(commandLineText.GetBytes(path[1..]));
        if (topic.AsSpan().IndexOfAnyInRange((byte)0x00, (byte)0x1F) >= 0)
        {
            throw new MqttTransferException(CurlExitCode.UrlMalformat, MqttTransferMessages.ControlByteInTopic);
        }

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
