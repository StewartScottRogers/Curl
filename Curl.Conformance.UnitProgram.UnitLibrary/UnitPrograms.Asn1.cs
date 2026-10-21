using Curl.Conformance.UnitProgram.Asn1.Der;
using Curl.Conformance.UnitProgram.Asn1.Names;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Asn1 area (BL-2842 to BL-2848); its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] Asn1Programs =>
    [
        new Tool1394CertParameter(),
        new Unit1397CertHostCheck(),
        new Unit1676DhPublicKey(),
        new Unit3205CipherSuiteNames(),
        new Unit1656GeneralizedTime(),
        new Unit1657IndefiniteNesting(),
        new Unit1666ObjectIdentifierEncoding(),
        new Unit1667ElementText(),
    ];
}
