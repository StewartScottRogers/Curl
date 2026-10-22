using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core.AltSvc;

namespace Curl.Conformance.UnitProgram.Headers.AltSvc;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1669.c</c> over <see cref="AltSvcCache" />: loads the case's
/// alt-svc file of 5010 entries and checks the cache holds exactly <c>MAX_ALTSVC_ENTRIES</c>,
/// <see cref="AltSvcCache.MaxEntries" />.
/// </summary>
/// <param name="clock">The clock; a <see cref="CurlTimeClock" /> at the case's <c>CURL_TIME</c> for the port itself.</param>
public sealed class Unit1669AltSvcEntryCap(TimeProvider clock) : IUnitProgram
{
    /// <inheritdoc />
    public string ProgramName => "unit1669";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        string path = AltSvcCacheFile.PathOf(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        AltSvcCache cache = new(clock);
        await AltSvcCacheFile.LoadAsync(cache, path, cancellationToken).ConfigureAwait(false);
        checks.FailUnless(cache.Entries.Count == AltSvcCache.MaxEntries, "wrong number of entries");
        return checks.Result;
    }
}
