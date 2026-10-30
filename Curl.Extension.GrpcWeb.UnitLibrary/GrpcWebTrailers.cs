using System.Text;

namespace Curl.Extension.GrpcWeb;

/// <summary>
/// Reads a trailers frame's payload: key-value pairs written as an HTTP/1 header block without the terminating empty
/// line (PROTOCOL-WEB.md, "Message framing"), lines ending in CRLF, or a bare LF as servers also write.
/// </summary>
internal static class GrpcWebTrailers
{
    /// <summary>Reads the fields of a trailers frame.</summary>
    /// <param name="payload">The frame's payload.</param>
    /// <returns>The fields in order, names in lower case and values trimmed.</returns>
    /// <exception cref="FormatException">A line has no colon.</exception>
    internal static IReadOnlyList<KeyValuePair<string, string>> Parse(ReadOnlySpan<byte> payload)
    {
        List<KeyValuePair<string, string>> fields = [];
        foreach (string line in Encoding.Latin1.GetString(payload).Split('\n'))
        {
            string field = line.TrimEnd('\r');
            if (field.Length == 0)
            {
                continue;
            }

            int colon = field.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                throw new FormatException($"--grpc-web: the reply's trailer line \"{field}\" has no colon (PROTOCOL-WEB.md, Message framing)");
            }

            fields.Add(new(field[..colon].Trim().ToLowerInvariant(), field[(colon + 1)..].Trim()));
        }

        return fields;
    }
}
