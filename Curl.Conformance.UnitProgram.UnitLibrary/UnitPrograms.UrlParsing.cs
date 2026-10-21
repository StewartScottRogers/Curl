using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the UrlParsing area (BL-2842 to BL-2848); its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] UrlParsingPrograms =>
    [
        new Unit1607ResolveParsing(),
        new Unit1614NoProxyMatching(),
        new Unit1663InterfaceParsing(),
        new Unit1961InetAddressText(),
        new Unit1675UrlApi(),
    ];
}
