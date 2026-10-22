namespace Curl.Networking;

internal sealed partial class DnsAnswerReader
{
    private const int MaximumHttpsRecords = 4;

    /// <summary>
    /// Keeps an HTTPS record's data undecoded, and ignores a CNAME's or a DNAME's; past the
    /// fourth, HTTPS records are silently ignored, as curl ignores them.
    /// </summary>
    private DnsMessageFailure StoreHttpsRecordData()
    {
        if (_recordType == (ushort)DnsRecordType.Https && HttpsRecordData.Count < MaximumHttpsRecords)
        {
            HttpsRecordData.Add(message.AsSpan(_index, _recordDataLength).ToArray());
        }

        return DnsMessageFailure.None;
    }
}
