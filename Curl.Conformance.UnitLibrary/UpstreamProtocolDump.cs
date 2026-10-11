namespace Curl.Conformance;

/// <summary>
/// What one emulated server writes to the protocol dump <c>&lt;verify&gt;&lt;protocol&gt;</c> compares,
/// each write numbered in the order it happened across every server of every case, so the dumps of
/// several servers can be put back into the one file upstream's servers all append to,
/// <c>%LOGDIR/server.input</c>: test2003's HTTP, FTP and TFTP lines interleave in the order curl
/// sent them, and FTP's <c>QUIT</c> at exit comes after the last HTTP request.
/// </summary>
/// <remarks>
/// Servers record on many threads at once when curl runs transfers in parallel, so every member
/// takes one lock, and the bytes of one write stay together.
/// </remarks>
internal sealed class UpstreamProtocolDump
{
    // Process-wide, so writes of different servers of one case are ordered against each other.
    private static long lastSequence;

    private readonly Lock gate = new();

    private readonly List<UpstreamProtocolDumpEntry> entries = [];

    /// <summary>Gets every byte recorded so far, in the order written.</summary>
    public byte[] Bytes
    {
        get
        {
            lock (gate)
            {
                return [.. entries.SelectMany(entry => entry.Bytes)];
            }
        }
    }

    /// <summary>Gets every write recorded so far, each with its place in the order of all writes.</summary>
    public IReadOnlyList<UpstreamProtocolDumpEntry> Entries
    {
        get
        {
            lock (gate)
            {
                return [.. entries];
            }
        }
    }

    /// <summary>
    /// Puts the dumps of several servers into one, as upstream's servers append to one file: the
    /// sources one after another when at most one holds anything, else every write in the order
    /// it happened.
    /// </summary>
    /// <param name="sources">Each server's bytes and the writes they came from.</param>
    /// <returns>The protocol dump.</returns>
    public static byte[] Merge(IReadOnlyList<(ReadOnlyMemory<byte> Bytes, IReadOnlyList<UpstreamProtocolDumpEntry> Entries)> sources) =>
        sources.Count(source => source.Bytes.Length > 0) > 1
            ? [.. sources.SelectMany(source => source.Entries).OrderBy(entry => entry.Sequence).SelectMany(entry => entry.Bytes)]
            : [.. sources.SelectMany(source => source.Bytes.ToArray())];

    /// <summary>Records one write.</summary>
    /// <param name="written">The bytes.</param>
    public void Record(ReadOnlySpan<byte> written)
    {
        lock (gate)
        {
            entries.Add(new UpstreamProtocolDumpEntry(Interlocked.Increment(ref lastSequence), written.ToArray()));
        }
    }

    /// <summary>Forgets the last bytes recorded, across writes from the last back.</summary>
    /// <param name="count">How many of the last bytes to forget; no more than were recorded.</param>
    public void Forget(int count)
    {
        lock (gate)
        {
            while (count > 0)
            {
                UpstreamProtocolDumpEntry last = entries[^1];
                int kept = Math.Max(0, last.Bytes.Length - count);
                count -= last.Bytes.Length - kept;
                entries.RemoveAt(entries.Count - 1);
                if (kept > 0)
                {
                    entries.Add(last with { Bytes = last.Bytes[..kept] });
                }
            }
        }
    }
}
