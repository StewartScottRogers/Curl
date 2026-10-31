using System.Globalization;
using System.Numerics;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Redis.Wire;

/// <summary>
/// Reads RESP3 replies off an <see cref="IConnection"/> ("RESP protocol description"): every type the specification
/// names, RESP2's <c>-1</c> null bulk string and null array, and an attribute dropped before the reply it describes.
/// A reply that breaks the specification throws <see cref="FormatException"/>; the connection ending inside one
/// throws <see cref="IOException"/>.
/// </summary>
internal sealed class RespReplyReader(IConnection transport, CancellationToken cancellationToken)
{
    /// <summary>How deep aggregates may nest before the reply is refused, so a hostile server cannot exhaust the stack.</summary>
    internal const int MaximumNestingDepth = 128;

    private static readonly Dictionary<byte, RespReplyKind> KindOfTypeByte = new()
    {
        [(byte)'+'] = RespReplyKind.SimpleString,
        [(byte)'-'] = RespReplyKind.SimpleError,
        [(byte)':'] = RespReplyKind.Integer,
        [(byte)'$'] = RespReplyKind.BulkString,
        [(byte)'*'] = RespReplyKind.Array,
        [(byte)'_'] = RespReplyKind.Null,
        [(byte)'#'] = RespReplyKind.Boolean,
        [(byte)','] = RespReplyKind.Double,
        [(byte)'('] = RespReplyKind.BigNumber,
        [(byte)'!'] = RespReplyKind.BulkError,
        [(byte)'='] = RespReplyKind.VerbatimString,
        [(byte)'%'] = RespReplyKind.Map,
        [(byte)'|'] = RespReplyKind.Attribute,
        [(byte)'~'] = RespReplyKind.Set,
        [(byte)'>'] = RespReplyKind.Push,
    };

    private static readonly HashSet<RespReplyKind> BlobKinds = [RespReplyKind.BulkString, RespReplyKind.BulkError, RespReplyKind.VerbatimString];

    private static readonly HashSet<RespReplyKind> AggregateKinds = [RespReplyKind.Array, RespReplyKind.Set, RespReplyKind.Push, RespReplyKind.Map];

    private readonly byte[] buffer = new byte[8192];

    private int start;

    private int end;

    /// <summary>Reads the next reply, or gives <see langword="null"/> when the connection ends before its first byte.</summary>
    internal async ValueTask<RespReply?> ReadAsync()
    {
        if (start == end && !await FillAsync().ConfigureAwait(false))
        {
            return null;
        }

        return await ReadReplyAsync(0).ConfigureAwait(false);
    }

    private static FormatException Malformed(string detail) => new($"Malformed RESP3 reply: {detail}");

    private async ValueTask<RespReply> ReadReplyAsync(int depth)
    {
        if (depth > MaximumNestingDepth)
        {
            throw Malformed("aggregates nested too deep");
        }

        byte[] line = await ReadLineAsync().ConfigureAwait(false);
        if (line.Length == 0 || !KindOfTypeByte.TryGetValue(line[0], out RespReplyKind kind))
        {
            throw Malformed("unknown type");
        }

        return await ReadAfterTypeLineAsync(kind, line[1..], depth).ConfigureAwait(false);
    }

    /// <summary>Reads the rest of a reply whose type line gave <paramref name="kind"/> and <paramref name="header"/>.</summary>
    private ValueTask<RespReply> ReadAfterTypeLineAsync(RespReplyKind kind, byte[] header, int depth)
    {
        if (kind == RespReplyKind.Attribute)
        {
            return ReadAfterAttributeAsync(header, depth);
        }

        if (BlobKinds.Contains(kind))
        {
            return ReadBlobAsync(kind, header);
        }

        return AggregateKinds.Contains(kind) ? ReadAggregateAsync(kind, header, depth) : ValueTask.FromResult(LineScalar(kind, header));
    }

    private static RespReply LineScalar(RespReplyKind kind, byte[] payload) =>
        IsValidLineScalar(kind, Encoding.ASCII.GetString(payload))
            ? RespReply.Scalar(kind, payload)
            : throw Malformed($"bad {kind} value");

    private static bool IsValidLineScalar(RespReplyKind kind, string text) => kind switch
    {
        RespReplyKind.Integer => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
        RespReplyKind.BigNumber => BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
        RespReplyKind.Boolean => text is "t" or "f",
        RespReplyKind.Null => text.Length == 0,
        RespReplyKind.Double => text is "inf" or "-inf" or "nan" || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        _ => true,
    };

    /// <summary>Gives a length or count, or -1 for RESP2's null; anything else throws.</summary>
    private static int LengthOf(byte[] header) =>
        int.TryParse(Encoding.ASCII.GetString(header), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int length) && length >= -1
            ? length
            : throw Malformed("bad length");

    private async ValueTask<RespReply> ReadBlobAsync(RespReplyKind kind, byte[] header)
    {
        int length = LengthOf(header);
        if (length < 0)
        {
            return RespReply.Scalar(RespReplyKind.Null, []);
        }

        byte[] payload = await ReadExactlyAsync(length).ConfigureAwait(false);
        if ((await ReadLineAsync().ConfigureAwait(false)).Length != 0)
        {
            throw Malformed("bulk data longer than its length");
        }

        return RespReply.Scalar(kind, kind == RespReplyKind.VerbatimString ? VerbatimText(payload) : payload);
    }

    /// <summary>Gives a verbatim string's text after its three-byte format and colon ("Verbatim strings").</summary>
    private static byte[] VerbatimText(byte[] payload) =>
        payload.Length >= 4 && payload[3] == (byte)':' ? payload[4..] : throw Malformed("verbatim string without a format");

    private async ValueTask<RespReply> ReadAggregateAsync(RespReplyKind kind, byte[] header, int depth)
    {
        int count = LengthOf(header);
        if (count < 0)
        {
            return RespReply.Scalar(RespReplyKind.Null, []);
        }

        List<RespReply> elements = await ReadElementsAsync(kind == RespReplyKind.Map ? 2L * count : count, depth + 1).ConfigureAwait(false);
        return new RespReply(kind, [], elements);
    }

    private async ValueTask<RespReply> ReadAfterAttributeAsync(byte[] header, int depth)
    {
        int count = LengthOf(header);
        await ReadElementsAsync(2L * Math.Max(count, 0), depth + 1).ConfigureAwait(false);
        return await ReadReplyAsync(depth).ConfigureAwait(false);
    }

    private async ValueTask<List<RespReply>> ReadElementsAsync(long count, int depth)
    {
        List<RespReply> elements = [];
        for (long index = 0; index < count; index++)
        {
            elements.Add(await ReadReplyAsync(depth).ConfigureAwait(false));
        }

        return elements;
    }

    private async ValueTask<byte[]> ReadLineAsync()
    {
        using MemoryStream line = new();
        while (true)
        {
            int newline = Array.IndexOf(buffer, (byte)'\n', start, end - start);
            if (newline >= 0)
            {
                line.Write(buffer, start, newline - start + 1);
                start = newline + 1;
                return LineWithoutEnd(line.ToArray());
            }

            line.Write(buffer, start, end - start);
            start = end;
            await FillOrThrowAsync().ConfigureAwait(false);
        }
    }

    private static byte[] LineWithoutEnd(byte[] line) =>
        line.Length >= 2 && line[^2] == (byte)'\r' ? line[..^2] : throw Malformed("line not ended by CR LF");

    private async ValueTask<byte[]> ReadExactlyAsync(int length)
    {
        byte[] payload = new byte[length];
        int filled = 0;
        while (filled < length)
        {
            if (start == end)
            {
                await FillOrThrowAsync().ConfigureAwait(false);
            }

            int count = Math.Min(length - filled, end - start);
            buffer.AsSpan(start, count).CopyTo(payload.AsSpan(filled));
            start += count;
            filled += count;
        }

        return payload;
    }

    private async ValueTask FillOrThrowAsync()
    {
        if (!await FillAsync().ConfigureAwait(false))
        {
            throw new IOException("The connection ended inside a RESP3 reply.");
        }
    }

    private async ValueTask<bool> FillAsync()
    {
        start = 0;
        end = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return end > 0;
    }
}
