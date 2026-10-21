using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.Limits;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1606.c</c>: the low speed check, <c>pgrs_speedcheck</c>, here
/// <see cref="LowSpeedWatchdog"/> on a <see cref="SteppedTimeProvider"/>. Each of the six rows is the C
/// program's <c>runawhile(time_limit, speed_limit, speed, dec)</c>: one check a second from second 1 to
/// second 99, the speed falling by <c>dec</c> after each, stopping at the check that finds the transfer too
/// slow, and the answer is the second before that check (99 when none does). The rows and their failure
/// lines, in order: (41, 41, 40, 0) is 41, (21, 21, 20, 0) is 21, (60, 60, 40, 0) is 60, (50, 50, 40, 0) is 50,
/// (40, 40, 40, 0) is 99, and (10, 50, 100, 2) is 36.
/// </summary>
/// <remarks>
/// The C program fakes <c>progress.current_speed</c>; the watchdog measures its speed from the bytes written
/// through <see cref="LowSpeedWatchdog.WatchOutput"/> over the last five seconds (fewer at the start), so
/// before each check the port writes the bytes that make that measure the row's speed for the second.
/// </remarks>
/// <param name="createWatchdog">
/// Starts the watchdog for a speed limit in bytes per second and a speed time on a clock;
/// <see cref="LowSpeedWatchdog"/>'s constructor unless a test injects another.
/// </param>
public sealed class Unit1606SpeedCheck(Func<long, TimeSpan, TimeProvider, LowSpeedWatchdog> createWatchdog) : IUnitProgram
{
    private const int LastCounter = 100;

    private const int SecondsMeasuredAcross = 5;

    private static readonly (int TimeLimit, int SpeedLimit, int Speed, int Decrement, int Expected, string Message)[] Rows =
    [
        (41, 41, 40, 0, 41, "wrong low speed timeout"),
        (21, 21, 20, 0, 21, "wrong low speed timeout"),
        (60, 60, 40, 0, 60, "wrong log speed timeout"),
        (50, 50, 40, 0, 50, "wrong log speed timeout"),
        (40, 40, 40, 0, 99, "should not time out"),
        (10, 50, 100, 2, 36, "bad timeout"),
    ];

    /// <summary>Creates the port over <see cref="LowSpeedWatchdog"/>.</summary>
    public Unit1606SpeedCheck()
        : this((bytesPerSecond, speedTime, clock) => new LowSpeedWatchdog(bytesPerSecond, speedTime, clock))
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1606";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((int timeLimit, int speedLimit, int speed, int decrement, int expected, string message) in Rows)
        {
            checks.FailUnless(RunAWhile(timeLimit, speedLimit, speed, decrement) == expected, message);
        }

        return Task.FromResult(checks.Result);
    }

    private int RunAWhile(int timeLimit, int speedLimit, int speed, int decrement)
    {
        SteppedTimeProvider clock = new();
        using LowSpeedWatchdog watchdog = createWatchdog(speedLimit, TimeSpan.FromSeconds(timeLimit), clock);
        using Stream output = watchdog.WatchOutput(Stream.Null);
        List<long> writtenBySecond = [0];
        int counter = 1;
        do
        {
            WriteForSpeed(output, writtenBySecond, speed);
            clock.Step(TimeSpan.FromSeconds(1));
            if (watchdog.IsTooSlow)
            {
                break;
            }

            counter++;
            speed -= decrement;
        }
        while (counter < LastCounter);

        return counter - 1;
    }

    // The watchdog's speed at second n is the bytes written since second max(0, n - 5) over the seconds between.
    private static void WriteForSpeed(Stream output, List<long> writtenBySecond, int speed)
    {
        int second = writtenBySecond.Count;
        int window = Math.Min(second, SecondsMeasuredAcross);
        long total = writtenBySecond[second - window] + ((long)window * speed);
        output.Write(new byte[total - writtenBySecond[^1]]);
        writtenBySecond.Add(total);
    }
}
