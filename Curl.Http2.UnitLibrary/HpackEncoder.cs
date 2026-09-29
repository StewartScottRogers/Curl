using static Curl.Http2.HpackPrimitives;

namespace Curl.Http2;

/// <summary>
/// Encodes header lists into HPACK header blocks (RFC 7541), choosing each field's
/// representation as nghttp2's deflater does, so the bytes on the wire match curl's
/// (ADR-0147). One encoder serves one connection's direction: its dynamic table carries
/// over from block to block, so blocks must be sent in the order they were encoded.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A field whose name and value match a table entry is sent as that index.</item>
/// <item><c>authorization</c>, <c>proxy-authorization</c>, a <c>cookie</c> shorter than 20
/// bytes, and any field marked <see cref="HeaderField.IsNeverIndexed" /> are literals never
/// indexed, and never sent as an index either.</item>
/// <item><c>:path</c>, <c>age</c>, <c>content-length</c>, <c>etag</c>,
/// <c>if-modified-since</c>, <c>if-none-match</c>, <c>location</c>, <c>set-cookie</c>, and a
/// field bigger than three quarters of the table are literals without indexing.</item>
/// <item>Every other field is a literal with incremental indexing.</item>
/// <item>A literal names its field by index when a table has the name: an exact dynamic
/// match first, then the static table, then the newest dynamic entry with the name.</item>
/// <item>A string is Huffman-coded when that is strictly shorter.</item>
/// </list>
/// </remarks>
public sealed class HpackEncoder
{
    /// <summary>The largest dynamic table nghttp2's deflater will use, whatever the peer allows.</summary>
    public const int DefaultMaximumTableSize = 4096;

    private static readonly HashSet<string> NamesNeverIndexed = ["authorization", "proxy-authorization"];

    private static readonly HashSet<string> NamesNotIndexed =
    [
        ":path", "age", "content-length", "etag", "if-modified-since", "if-none-match", "location", "set-cookie",
    ];

    private const int ShortestIndexedCookieLength = 20;

    /// <summary>First-byte pattern of a literal with incremental indexing (RFC 7541 section 6.2.1).</summary>
    private const byte IncrementalIndexingFlags = 0x40;

    /// <summary>First-byte pattern of a literal without indexing (RFC 7541 section 6.2.2).</summary>
    private const byte WithoutIndexingFlags = 0x00;

    /// <summary>First-byte pattern of a literal never indexed (RFC 7541 section 6.2.3).</summary>
    private const byte NeverIndexedFlags = 0x10;

    private readonly HpackDynamicTable dynamicTable;

    private readonly int largestMaximumTableSize;

    /// <summary>The smallest size the table passed through since the last block, if it changed.</summary>
    private int smallestMaximumTableSizeSinceLastBlock = int.MaxValue;

    private bool isTableSizeUpdatePending;

    /// <summary>
    /// Initializes a new instance of the <see cref="HpackEncoder" /> class.
    /// </summary>
    /// <param name="maximumTableSize">
    /// The largest dynamic table this encoder will use, whatever the peer's
    /// SETTINGS_HEADER_TABLE_SIZE; nghttp2 uses 4096. The table starts at this size, which
    /// the peer must allow: HTTP/2's initial 4096 does.
    /// </param>
    public HpackEncoder(int maximumTableSize = DefaultMaximumTableSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableSize);
        largestMaximumTableSize = maximumTableSize;
        dynamicTable = new HpackDynamicTable(maximumTableSize);
    }

    /// <summary>Gets the dynamic table's current maximum size.</summary>
    public int MaximumTableSize => dynamicTable.MaximumSize;

    /// <summary>Gets the dynamic table's current size (RFC 7541 section 4.1).</summary>
    public int TableSize => dynamicTable.Size;

    /// <summary>
    /// Takes a new SETTINGS_HEADER_TABLE_SIZE from the peer. The table's maximum becomes the
    /// smaller of it and the encoder's own limit at once, and the next block opens with the
    /// size updates that tell the peer: the smallest size passed through, when that is below
    /// the final one, then the final one (RFC 7541 section 4.2).
    /// </summary>
    /// <param name="peerMaximumTableSize">The peer's SETTINGS_HEADER_TABLE_SIZE.</param>
    public void SetPeerMaximumTableSize(int peerMaximumTableSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(peerMaximumTableSize);
        var size = Math.Min(peerMaximumTableSize, largestMaximumTableSize);
        dynamicTable.Resize(size);
        smallestMaximumTableSizeSinceLastBlock = Math.Min(smallestMaximumTableSizeSinceLastBlock, size);
        isTableSizeUpdatePending = true;
    }

    /// <summary>
    /// Encodes one header list into a header block.
    /// </summary>
    /// <param name="fields">The header list, in order.</param>
    /// <returns>The header block.</returns>
    public byte[] Encode(IEnumerable<HeaderField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var output = new List<byte>();
        WritePendingTableSizeUpdates(output);
        foreach (var field in fields)
        {
            WriteField(output, field);
        }

        return [.. output];
    }

    private void WritePendingTableSizeUpdates(List<byte> output)
    {
        if (!isTableSizeUpdatePending)
        {
            return;
        }

        if (smallestMaximumTableSizeSinceLastBlock < dynamicTable.MaximumSize)
        {
            WriteInteger(output, smallestMaximumTableSizeSinceLastBlock, 5, 0x20);
        }

        WriteInteger(output, dynamicTable.MaximumSize, 5, 0x20);
        isTableSizeUpdatePending = false;
        smallestMaximumTableSizeSinceLastBlock = int.MaxValue;
    }

    private void WriteField(List<byte> output, HeaderField field)
    {
        var isNeverIndexed = IsNeverIndexed(field);
        var (index, isExact) = Find(field, nameOnly: isNeverIndexed);
        if (isExact)
        {
            WriteInteger(output, index, 7, 0x80);
            return;
        }

        var (prefixBits, flags) = ChooseLiteral(field, isNeverIndexed);
        if (flags == IncrementalIndexingFlags)
        {
            dynamicTable.Add(field);
        }

        WriteInteger(output, index, prefixBits, flags);
        if (index == 0)
        {
            WriteString(output, field.Name);
        }

        WriteString(output, field.Value);
    }

    private (int PrefixBits, byte Flags) ChooseLiteral(HeaderField field, bool isNeverIndexed)
    {
        if (isNeverIndexed)
        {
            return (4, NeverIndexedFlags);
        }

        var isIndexed = !NamesNotIndexed.Contains(field.Name) && field.Size <= dynamicTable.MaximumSize * 3 / 4;
        return isIndexed ? (6, IncrementalIndexingFlags) : (4, WithoutIndexingFlags);
    }

    private (int Index, bool IsExact) Find(HeaderField field, bool nameOnly)
    {
        var (dynamicIndex, isDynamicExact) = dynamicTable.Find(field.Name, field.Value, nameOnly);
        if (isDynamicExact)
        {
            return (HpackStaticTable.Count + dynamicIndex, true);
        }

        var (staticIndex, isStaticExact) = HpackStaticTable.Find(field.Name, field.Value, nameOnly);
        if (staticIndex != 0 || dynamicIndex == 0)
        {
            return (staticIndex, isStaticExact);
        }

        return (HpackStaticTable.Count + dynamicIndex, false);
    }

    private static bool IsNeverIndexed(HeaderField field) =>
        field.IsNeverIndexed
        || NamesNeverIndexed.Contains(field.Name)
        || (field.Name == "cookie" && field.Value.Length < ShortestIndexedCookieLength);
}
