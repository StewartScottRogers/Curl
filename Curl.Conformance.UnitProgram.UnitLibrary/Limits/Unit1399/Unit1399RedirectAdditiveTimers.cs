using Curl.Api.Info;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.Limits.Unit1399;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1399.c</c>: <c>Curl_pgrsTime</c>'s phase timers add up
/// across redirect hops, here <see cref="RedirectHopTimings"/> merging the hops and
/// <see cref="WriteOutTransferInfo.FromReport"/> turning the merge into the times <c>-w</c> prints. The first
/// hop reaches name lookup, connect, TLS handshake, pretransfer and first byte 2 s after it starts; after
/// 1 s spent redirecting, the second hop reaches them all 1 s after it starts. The 10 checks, in order, each
/// failing with <c>about N seconds should have passed</c>: after the first hop, <c>t_nslookup</c>,
/// <c>t_connect</c>, <c>t_appconnect</c>, <c>t_pretransfer</c> and <c>t_starttransfer</c> are each 2 s;
/// after the second, the same five are each 3 s.
/// </summary>
/// <remarks>
/// As the C program does, each check first writes <c>is &lt;us&gt; us same as &lt;n&gt; seconds? Yes</c> (or
/// <c>No</c>) to standard error. The C program also calls <c>Curl_pgrsTime(TIMER_STARTTRANSFER)</c> twice on
/// the second hop to show only the first call counts; a hop's <see cref="TransferTimings.FirstByteReceived"/>
/// is one timestamp its handler takes once, so that call has no counterpart.
/// </remarks>
/// <param name="mergeHops">
/// Merges the hops' timings, in order, on a clock into the chain's timings; <see cref="RedirectHopTimings"/>
/// unless a test injects another.
/// </param>
public sealed class Unit1399RedirectAdditiveTimers(Func<IReadOnlyList<TransferTimings>, TimeProvider, TransferTimings> mergeHops) : IUnitProgram
{
    private static readonly TimeSpan FirstHopPhaseTime = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan RedirectTime = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan SecondHopPhaseTime = TimeSpan.FromSeconds(1);

    /// <summary>Creates the port over <see cref="RedirectHopTimings"/>.</summary>
    public Unit1399RedirectAdditiveTimers()
        : this(MergeWithRedirectHopTimings)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1399";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        SteppedTimeProvider clock = new();
        TransferTimings firstHop = RunHop(clock, FirstHopPhaseTime);
        ExpectTimerSeconds(checks, context.StandardError, Times([firstHop], clock), 2);

        clock.Step(RedirectTime);
        TransferTimings secondHop = RunHop(clock, SecondHopPhaseTime);
        ExpectTimerSeconds(checks, context.StandardError, Times([firstHop, secondHop], clock), 3);
        return Task.FromResult(checks.Result);
    }

    private static TransferTimings MergeWithRedirectHopTimings(IReadOnlyList<TransferTimings> hops, TimeProvider clock)
    {
        RedirectHopTimings timings = new(clock);
        foreach (TransferTimings hop in hops)
        {
            timings.Add(hop);
        }

        return timings.Merge(hops[^1])!;
    }

    // One hop that reaches every phase phaseTime after it starts, the C program's fake t_startsingle.
    private static TransferTimings RunHop(SteppedTimeProvider clock, TimeSpan phaseTime)
    {
        long started = clock.GetTimestamp();
        clock.Step(phaseTime);
        long reached = clock.GetTimestamp();
        return new TransferTimings(started, new ConnectTimings(started, reached, reached, reached), reached, reached, reached, reached);
    }

    private TransferInfo Times(IReadOnlyList<TransferTimings> hops, TimeProvider clock)
    {
        TransferResult result = TransferResult.Success(0) with { Report = new TransferReport { Timings = mergeHops(hops, clock) } };
        return WriteOutTransferInfo.FromReport(result, "http://example.com/", clock);
    }

    private static void ExpectTimerSeconds(UnitChecks checks, TextWriter standardError, TransferInfo info, int seconds)
    {
        string message = $"about {seconds} seconds should have passed";
        foreach (TimeSpan time in (TimeSpan[])[info.NameLookupTime, info.ConnectTime, info.AppConnectTime, info.PreTransferTime, info.StartTransferTime])
        {
            checks.FailUnless(MicrosecondsMatchSeconds(standardError, time, seconds), message);
        }
    }

    // usec_matches_seconds: whole seconds of the microseconds, written to standard error as the C program does.
    private static bool MicrosecondsMatchSeconds(TextWriter standardError, TimeSpan time, int expectedSeconds)
    {
        long microseconds = time.Ticks / TimeSpan.TicksPerMicrosecond;
        bool same = microseconds / 1_000_000 == expectedSeconds;
        standardError.WriteLine($"is {microseconds} us same as {expectedSeconds} seconds? {(same ? "Yes" : "No")}");
        return same;
    }
}
