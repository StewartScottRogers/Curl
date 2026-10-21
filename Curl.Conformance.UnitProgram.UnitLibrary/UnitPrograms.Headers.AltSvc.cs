using Curl.Conformance.UnitProgram.Headers.AltSvc;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Headers area's AltSvc subarea; its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] AltSvcPrograms =>
    [
        new Unit1654AltSvcParse(Unit1654AltSvcParse.UpstreamRows, new CurlTimeClock(CurlTimeClock.UpstreamCaseEpochSeconds)),
        new Unit1669AltSvcEntryCap(new CurlTimeClock(CurlTimeClock.UpstreamCaseEpochSeconds)),
    ];
}
