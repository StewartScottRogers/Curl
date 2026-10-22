namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>
    /// The ported unit programs of the Headers area, one array per subarea, each in its own file
    /// <c>UnitPrograms.Headers.&lt;Subarea&gt;.cs</c>, so each subarea's port task touches only its own file.
    /// </summary>
    private static IUnitProgram[] HeadersPrograms =>
        [.. HeaderValuesPrograms, .. NetrcPrograms, .. AltSvcPrograms, .. HstsPrograms, .. Http1Programs, .. CapsulePrograms];
}
