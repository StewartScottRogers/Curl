using System.Net;

namespace Curl.Networking;

internal sealed partial class DnsAnswerReader
{
    private const int MaximumAddresses = 24;

    private DnsMessageFailure StoreAddress(int addressLength)
    {
        if (_recordDataLength != addressLength)
        {
            return DnsMessageFailure.RdataLength;
        }

        if (Addresses.Count < MaximumAddresses)
        {
            Addresses.Add(new IPAddress(message.AsSpan(_index, addressLength)));
        }

        return DnsMessageFailure.None;
    }
}
