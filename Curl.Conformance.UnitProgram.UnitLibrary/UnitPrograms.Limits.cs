using Curl.Conformance.UnitProgram.Limits;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Limits area (BL-2842 to BL-2848); its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] LimitsPrograms =>
    [
        new Unit1605NegativeLengths(),
        new Unit1606SpeedCheck(),
        new Unit1303TimeLeft(),
    ];
}
