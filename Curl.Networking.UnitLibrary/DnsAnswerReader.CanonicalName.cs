using System.Text;

namespace Curl.Networking;

internal sealed partial class DnsAnswerReader
{
    private const int MaximumCanonicalNames = 4;

    private DnsMessageFailure StoreCanonicalName()
    {
        if (CanonicalNames.Count == MaximumCanonicalNames)
        {
            return DnsMessageFailure.None;
        }

        var name = new StringBuilder();
        var failure = FollowName(_index, name);
        CanonicalNames.Add(name.ToString());
        return failure;
    }
}
