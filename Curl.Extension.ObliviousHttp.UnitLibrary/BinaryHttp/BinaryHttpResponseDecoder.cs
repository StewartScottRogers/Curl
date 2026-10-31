using System.Text;

namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>
/// Decodes a Binary HTTP response (RFC 9292): framing indicator 1 (known length, section 3.1)
/// or 3 (indeterminate length, section 3.2), any informational (1xx) responses with their
/// header sections, the final status code, header section, content and trailer section, and
/// zero-valued padding. Content and trailers the message truncates are empty (section 3.8).
/// </summary>
public static class BinaryHttpResponseDecoder
{
    private const long KnownLengthResponseFramingIndicator = 1;
    private const long IndeterminateLengthResponseFramingIndicator = 3;

    /// <summary>Decodes <paramref name="message" />.</summary>
    /// <returns>The response it holds.</returns>
    /// <exception cref="BinaryHttpFormatException">The message is not a valid Binary HTTP response.</exception>
    public static BinaryHttpResponse Decode(ReadOnlySpan<byte> message)
    {
        BinaryHttpReader reader = new(message);
        bool knownLength = ReadFramingIndicator(ref reader) == KnownLengthResponseFramingIndicator;
        List<BinaryHttpInformationalResponse> informationalResponses = [];
        int statusCode = ReadStatusCode(ref reader);
        while (statusCode < 200)
        {
            informationalResponses.Add(new(statusCode, ReadFieldSection(ref reader, knownLength)));
            statusCode = ReadStatusCode(ref reader);
        }

        List<BinaryHttpField> headers = ReadFieldSection(ref reader, knownLength);
        byte[] content = reader.IsAtEnd ? [] : ReadContent(ref reader, knownLength);
        List<BinaryHttpField> trailers = reader.IsAtEnd ? [] : ReadFieldSection(ref reader, knownLength);
        reader.ReadPadding();
        return new(informationalResponses, statusCode, headers, content, trailers);
    }

    private static long ReadFramingIndicator(ref BinaryHttpReader reader)
    {
        long framingIndicator = reader.ReadVariableLengthInteger();
        return framingIndicator is KnownLengthResponseFramingIndicator or IndeterminateLengthResponseFramingIndicator
            ? framingIndicator
            : throw new BinaryHttpFormatException(
                $"Binary HTTP framing indicator {framingIndicator} is not a response's (1 or 3).");
    }

    private static int ReadStatusCode(ref BinaryHttpReader reader)
    {
        long statusCode = reader.ReadVariableLengthInteger();
        return statusCode is >= 100 and <= 599
            ? (int)statusCode
            : throw new BinaryHttpFormatException($"Binary HTTP status code {statusCode} is not 100 to 599.");
    }

    private static List<BinaryHttpField> ReadFieldSection(ref BinaryHttpReader reader, bool knownLength)
    {
        return knownLength ? ReadKnownLengthFieldSection(ref reader) : ReadIndeterminateLengthFieldSection(ref reader);
    }

    private static List<BinaryHttpField> ReadKnownLengthFieldSection(ref BinaryHttpReader reader)
    {
        BinaryHttpReader section = new(reader.ReadLengthPrefixed());
        List<BinaryHttpField> fields = [];
        while (!section.IsAtEnd)
        {
            ReadOnlySpan<byte> name = section.ReadLengthPrefixed();
            if (name.IsEmpty)
            {
                throw new BinaryHttpFormatException("A Binary HTTP field line has an empty name.");
            }

            fields.Add(new(Encoding.Latin1.GetString(name), Encoding.Latin1.GetString(section.ReadLengthPrefixed())));
        }

        return fields;
    }

    private static List<BinaryHttpField> ReadIndeterminateLengthFieldSection(ref BinaryHttpReader reader)
    {
        List<BinaryHttpField> fields = [];
        for (ReadOnlySpan<byte> name = reader.ReadLengthPrefixed(); !name.IsEmpty; name = reader.ReadLengthPrefixed())
        {
            fields.Add(new(Encoding.Latin1.GetString(name), Encoding.Latin1.GetString(reader.ReadLengthPrefixed())));
        }

        return fields;
    }

    private static byte[] ReadContent(ref BinaryHttpReader reader, bool knownLength)
    {
        if (knownLength)
        {
            return reader.ReadLengthPrefixed().ToArray();
        }

        using MemoryStream content = new();
        for (ReadOnlySpan<byte> chunk = reader.ReadLengthPrefixed(); !chunk.IsEmpty; chunk = reader.ReadLengthPrefixed())
        {
            content.Write(chunk);
        }

        return content.ToArray();
    }
}
