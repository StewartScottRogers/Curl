using Curl.Conformance.UnitProgram.Headers.Capsule;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Headers area's Capsule subarea; its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] CapsulePrograms =>
    [
        new Unit3400Capsule(),
    ];
}
