using System.Text;

namespace Curl.Networking;

internal sealed partial class DnsAnswerReader
{
    /// <summary>
    /// Stores an SRV record's priority, weight, port and target (RFC 2782); data too short to
    /// hold the three numbers and a name is <see cref="DnsMessageFailure.RdataLength" />.
    /// </summary>
    private DnsMessageFailure StoreServiceRecord()
    {
        if (_recordDataLength < 7)
        {
            return DnsMessageFailure.RdataLength;
        }

        var target = new StringBuilder();
        var failure = FollowName(_index + 6, target);
        ServiceRecords.Add(new DnsServiceRecord(ReadUInt16At(_index), ReadUInt16At(_index + 2), ReadUInt16At(_index + 4), target.ToString()));
        return failure;
    }
}
