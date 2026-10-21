using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.Limits;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3216.c</c>: the <c>Curl_rlimit_*</c> token bucket, here
/// <see cref="TransferRateLimit"/> on a <see cref="SteppedTimeProvider"/> that stands for the C program's
/// <c>struct curltime ts</c>. Its 30 checks, in order, by failure line: unlimited - "inf", "drain keep inf",
/// "inf never waits", "inf blocked to 0", "blocked inf", "unblocked unlimited"; 10 a second - "initial 10",
/// "drain to 5", "drain to 2", "drain to -1", "wait 1099ms", "wait 1098ms", "10 inc per sec",
/// "10 inc per sec(2)", "10 blocked to 0", "unblocked 10"; 10 a second with a burst of 15 - "initial 10",
/// "drain to 5", "drain to 2", "drain to -1", "10 inc per sec", "10/15 burst limit", "10/15 burst limit(2)",
/// "drain to 0", "wait 1 sec", "0 after 0.5 sec", "wait 0.5 sec", "10 after 1.5 sec", "wait 0",
/// "10 after 2 sec".
/// </summary>
/// <remarks>
/// Where the C program sets <c>ts = curlx_now()</c> before blocking the 10-a-second limit, the port leaves
/// the clock where it is: blocking and unblocking read the time only to start afresh from it.
/// </remarks>
/// <param name="createLimit">
/// Starts a limit for a rate and a burst a second on a clock; <see cref="TransferRateLimit"/>'s constructor
/// unless a test injects another.
/// </param>
public sealed class Unit3216RateLimit(Func<long, long, TimeProvider, TransferRateLimit> createLimit) : IUnitProgram
{
    /// <summary>Creates the port over <see cref="TransferRateLimit"/>.</summary>
    public Unit3216RateLimit()
        : this((ratePerSecond, burstPerSecond, clock) => new TransferRateLimit(ratePerSecond, burstPerSecond, clock))
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit3216";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        CheckUnlimited(checks);
        CheckTenASecond(checks);
        CheckTenASecondWithBurst(checks);
        return Task.FromResult(checks.Result);
    }

    private void CheckUnlimited(UnitChecks checks)
    {
        TransferRateLimit limit = createLimit(0, 0, new SteppedTimeProvider());
        checks.FailUnless(limit.Available() == long.MaxValue, "inf");
        limit.Drain(1_000_000);
        checks.FailUnless(limit.Available() == long.MaxValue, "drain keep inf");
        checks.FailUnless(limit.WaitMilliseconds() == 0, "inf never waits");

        limit.Block(true);
        checks.FailUnless(limit.Available() == 0, "inf blocked to 0");
        limit.Drain(1_000_000);
        checks.FailUnless(limit.Available() == 0, "blocked inf");
        limit.Block(false);
        checks.FailUnless(limit.Available() == long.MaxValue, "unblocked unlimited");
    }

    private void CheckTenASecond(UnitChecks checks)
    {
        SteppedTimeProvider clock = new();
        TransferRateLimit limit = createLimit(10, 0, clock);
        checks.FailUnless(limit.Available() == 10, "initial 10");
        limit.Drain(5);
        checks.FailUnless(limit.Available() == 5, "drain to 5");
        limit.Drain(3);
        checks.FailUnless(limit.Available() == 2, "drain to 2");
        clock.Step(TimeSpan.FromMilliseconds(1));
        limit.Drain(3);
        checks.FailUnless(limit.Available() == -1, "drain to -1");
        checks.FailUnless(limit.WaitMilliseconds() == 1099, "wait 1099ms");
        clock.Step(TimeSpan.FromMilliseconds(1));
        checks.FailUnless(limit.WaitMilliseconds() == 1098, "wait 1098ms");
        clock.Step(TimeSpan.FromSeconds(1));
        checks.FailUnless(limit.Available() == 9, "10 inc per sec");
        clock.Step(TimeSpan.FromSeconds(1));
        checks.FailUnless(limit.Available() == 19, "10 inc per sec(2)");

        limit.Block(true);
        checks.FailUnless(limit.Available() == 0, "10 blocked to 0");
        limit.Block(false);
        checks.FailUnless(limit.Available() == 10, "unblocked 10");
    }

    private void CheckTenASecondWithBurst(UnitChecks checks)
    {
        SteppedTimeProvider clock = new();
        TransferRateLimit limit = createLimit(10, 15, clock);
        checks.FailUnless(limit.Available() == 10, "initial 10");
        limit.Drain(5);
        checks.FailUnless(limit.Available() == 5, "drain to 5");
        limit.Drain(3);
        checks.FailUnless(limit.Available() == 2, "drain to 2");
        limit.Drain(3);
        checks.FailUnless(limit.Available() == -1, "drain to -1");
        clock.Step(TimeSpan.FromSeconds(1));
        checks.FailUnless(limit.Available() == 9, "10 inc per sec");
        clock.Step(TimeSpan.FromSeconds(1));
        checks.FailUnless(limit.Available() == 15, "10/15 burst limit");
        clock.Step(TimeSpan.FromSeconds(1));
        checks.FailUnless(limit.Available() == 15, "10/15 burst limit(2)");
        limit.Drain(15);
        checks.FailUnless(limit.Available() == 0, "drain to 0");
        checks.FailUnless(limit.WaitMilliseconds() == 1000, "wait 1 sec");
        clock.Step(TimeSpan.FromMilliseconds(500));
        checks.FailUnless(limit.Available() == 0, "0 after 0.5 sec");
        checks.FailUnless(limit.WaitMilliseconds() == 500, "wait 0.5 sec");
        clock.Step(TimeSpan.FromSeconds(1));
        checks.FailUnless(limit.Available() == 10, "10 after 1.5 sec");
        checks.FailUnless(limit.WaitMilliseconds() == 0, "wait 0");
        clock.Step(TimeSpan.FromMilliseconds(500));
        checks.FailUnless(limit.Available() == 15, "10 after 2 sec");
    }
}
