using Curl.Conformance.UnitProgram.Headers.HeaderValues;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Headers area's HeaderValues subarea; its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] HeaderValuesPrograms =>
    [
        new Unit1625CompareHeader(Unit1625CompareHeader.UpstreamRows),
        new Unit1626CopyHeaderValue(Unit1626CopyHeaderValue.UpstreamRows),
    ];
}
