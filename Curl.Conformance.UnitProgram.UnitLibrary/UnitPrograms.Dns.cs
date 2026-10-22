using Curl.Conformance.UnitProgram.Dns.Easy;
using Curl.Conformance.UnitProgram.Dns.HttpsRecord;
using Curl.Conformance.UnitProgram.Dns.Shuffle;
using Curl.Conformance.UnitProgram.Dns.Tls;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Dns area (BL-2842 to BL-2848); its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] DnsPrograms =>
    [
        new Unit1658HttpsRecordDecoding(),
        new Unit3303MutualTlsConnectionReuse(),
        new Unit3304TlsSessionPeerKey(),
        new Unit1608DnsShuffle(),
        new Unit1652InfoLineTruncation(),
        new Unit2413ConnectionPeer(),
        new Unit3219ProtocolMask(),
        new Unit3302GssApiDelegation(),
    ];
}
