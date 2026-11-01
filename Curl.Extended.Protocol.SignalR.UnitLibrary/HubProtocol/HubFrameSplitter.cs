namespace Curl.Extended.Protocol.SignalR.HubProtocol;

/// <summary>
/// Splits the bytes a SignalR connection receives, however they arrive, into the JSON records that each end with the
/// record separator 0x1E (HubProtocol.md, "JSON Encoding").
/// </summary>
internal sealed class HubFrameSplitter
{
    /// <summary>The byte that ends every JSON hub protocol record.</summary>
    public const byte RecordSeparator = 0x1E;

    private readonly List<byte> pending = [];

    /// <summary>The bytes received after the last whole record.</summary>
    public int PendingByteCount => pending.Count;

    /// <summary>Adds bytes as received.</summary>
    public void Append(ReadOnlySpan<byte> received) => pending.AddRange(received);

    /// <summary>Takes the oldest whole record, without its separator, when one has arrived.</summary>
    public bool TryTakeRecord(out byte[] record)
    {
        int separatorIndex = pending.IndexOf(RecordSeparator);
        if (separatorIndex < 0)
        {
            record = [];
            return false;
        }

        record = pending.GetRange(0, separatorIndex).ToArray();
        pending.RemoveRange(0, separatorIndex + 1);
        return true;
    }
}
