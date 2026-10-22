using Curl.Conformance.UnitProgram.Headers.Hsts;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Headers area's Hsts subarea; its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] HstsPrograms =>
    [
        new Unit1660Hsts(Unit1660Hsts.UpstreamCurlTime, Unit1660Hsts.UpstreamRows),
        new Unit1674HstsEntryCap(Unit1660Hsts.UpstreamCurlTime, Unit1674HstsEntryCap.UpstreamEntriesWritten),
    ];
}
