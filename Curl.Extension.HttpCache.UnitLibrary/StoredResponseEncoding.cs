using System.Text;

namespace Curl.Extension.HttpCache;

/// <summary>
/// The bytes of one stored response in a cache entry file: a magic line, then with
/// <see cref="BinaryWriter"/> the status code, status line, request and response times as UTC
/// ticks, the header count and each name and value, and the body length and body. Bytes that do
/// not decode to exactly that, with nothing after the body, decode as no entry.
/// </summary>
public static class StoredResponseEncoding
{
    private static readonly byte[] Magic = "CURL-HTTP-CACHE 1\n"u8.ToArray();

    /// <summary>The entry file bytes of <paramref name="response"/>.</summary>
    public static byte[] Encode(StoredResponse response)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(response.StatusCode);
            writer.Write(response.StatusLine);
            writer.Write(response.RequestTime.UtcTicks);
            writer.Write(response.ResponseTime.UtcTicks);
            writer.Write(response.Headers.Count);
            foreach (var header in response.Headers)
            {
                writer.Write(header.Key);
                writer.Write(header.Value);
            }

            writer.Write(response.Body.Length);
            writer.Write(response.Body);
        }

        return stream.ToArray();
    }

    /// <summary>The response <paramref name="bytes"/> encode, or <see langword="null"/> when they are corrupt or truncated.</summary>
    public static StoredResponse? Decode(byte[] bytes)
    {
        if (!bytes.AsSpan().StartsWith(Magic))
        {
            return null;
        }

        try
        {
            return DecodeAfterMagic(bytes);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static StoredResponse? DecodeAfterMagic(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, Magic.Length, bytes.Length - Magic.Length);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var statusCode = reader.ReadInt32();
        var statusLine = reader.ReadString();
        var requestTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
        var responseTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
        var headerCount = reader.ReadInt32();
        var headers = new List<KeyValuePair<string, string>>();
        for (var index = 0; index < headerCount; index++)
        {
            headers.Add(new(reader.ReadString(), reader.ReadString()));
        }

        var bodyLength = reader.ReadInt32();
        var body = reader.ReadBytes(bodyLength);
        var isWhole = body.Length == bodyLength && stream.Position == stream.Length;
        return isWhole ? new StoredResponse(statusLine, statusCode, headers, body, requestTime, responseTime) : null;
    }
}
