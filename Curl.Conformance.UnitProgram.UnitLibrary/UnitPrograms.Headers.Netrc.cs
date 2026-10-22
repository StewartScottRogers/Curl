using Curl.Conformance.UnitProgram.Headers.Netrc;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Headers area's Netrc subarea; its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] NetrcPrograms =>
    [
        new Unit1304NetrcParsing(),
    ];
}
