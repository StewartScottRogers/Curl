using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.Limits;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1303.c</c>: <c>Curl_timeleft_ms</c>, here
/// <see cref="TransferTimeLeft.Milliseconds"/>, for a transfer started at second 1000000. Each of its 33 rows
/// sets the time now, <c>-m</c> and <c>--connect-timeout</c> in milliseconds (0 for none) and whether the
/// transfer is still connecting, and fails with the row's comment unless the time left is the row's answer.
/// The rows, in order, in groups of four at 4 s, 4.99 s, 10 s and 12 s after the start: both timeouts set
/// (10000, 8000) not connecting (6000, 5010, -1, -2000) and connecting (4000, 3010, at 8 s -1, at 10 s -2000);
/// only <c>-m</c> (10000, 0) not connecting and connecting (6000, 5010, -1, -2000 each); only the connect
/// timeout (0, 10000) not connecting (0 four times, "no timeout active") and connecting (6000, 5010, -1,
/// -2000); no timeout (0, 0) not connecting (0 four times) and connecting (296000, 295010, 290000, 288000,
/// "no timeout active"); and last, both set (10000, 12000) connecting at 4 s, 6000.
/// </summary>
/// <param name="timeLeft">
/// <c>Curl_timeleft_ms</c> from <c>-m</c>, <c>--connect-timeout</c>, whether connecting and the time since the
/// start; <see cref="TransferTimeLeft.Milliseconds"/> unless a test injects another.
/// </param>
public sealed class Unit1303TimeLeft(Func<TimeSpan, TimeSpan, bool, TimeSpan, long> timeLeft) : IUnitProgram
{
    private const string SixSeconds = "6 seconds should be left";

    private const string FiveThousandTen = "5010 ms should be left";

    private const string Expired = "timeout is -1, expired";

    private const string Overdue = "-2000, overdue 2 seconds";

    private const string NoTimeout = "no timeout active";

    private static readonly (int NowSeconds, int NowMicroseconds, int TimeoutMs, int ConnectTimeoutMs, bool Connecting, long Result, string Comment)[] Rows =
    [
        (4, 0, 10000, 8000, false, 6000, SixSeconds),
        (4, 990000, 10000, 8000, false, 5010, FiveThousandTen),
        (10, 0, 10000, 8000, false, -1, Expired),
        (12, 0, 10000, 8000, false, -2000, Overdue),

        (4, 0, 10000, 8000, true, 4000, "4 seconds should be left"),
        (4, 990000, 10000, 8000, true, 3010, "3010 ms should be left"),
        (8, 0, 10000, 8000, true, -1, Expired),
        (10, 0, 10000, 8000, true, -2000, Overdue),

        (4, 0, 10000, 0, false, 6000, SixSeconds),
        (4, 990000, 10000, 0, false, 5010, FiveThousandTen),
        (10, 0, 10000, 0, false, -1, Expired),
        (12, 0, 10000, 0, false, -2000, Overdue),

        (4, 0, 10000, 0, true, 6000, SixSeconds),
        (4, 990000, 10000, 0, true, 5010, FiveThousandTen),
        (10, 0, 10000, 0, true, -1, Expired),
        (12, 0, 10000, 0, true, -2000, Overdue),

        (4, 0, 0, 10000, false, 0, NoTimeout),
        (4, 990000, 0, 10000, false, 0, NoTimeout),
        (10, 0, 0, 10000, false, 0, NoTimeout),
        (12, 0, 0, 10000, false, 0, NoTimeout),

        (4, 0, 0, 10000, true, 6000, SixSeconds),
        (4, 990000, 0, 10000, true, 5010, FiveThousandTen),
        (10, 0, 0, 10000, true, -1, Expired),
        (12, 0, 0, 10000, true, -2000, Overdue),

        (4, 0, 0, 0, false, 0, NoTimeout),
        (4, 990000, 0, 0, false, 0, NoTimeout),
        (10, 0, 0, 0, false, 0, NoTimeout),
        (12, 0, 0, 0, false, 0, NoTimeout),

        (4, 0, 0, 0, true, 296000, NoTimeout),
        (4, 990000, 0, 0, true, 295010, NoTimeout),
        (10, 0, 0, 0, true, 290000, NoTimeout),
        (12, 0, 0, 0, true, 288000, NoTimeout),

        (4, 0, 10000, 12000, true, 6000, SixSeconds),
    ];

    /// <summary>Creates the port over <see cref="TransferTimeLeft"/>.</summary>
    public Unit1303TimeLeft()
        : this(TransferTimeLeft.Milliseconds)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1303";

    /// <summary>The number of rows the C program checks, 33.</summary>
    public static int RowCount => Rows.Length;

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((int nowSeconds, int nowMicroseconds, int timeoutMs, int connectTimeoutMs, bool connecting, long result, string comment) in Rows)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(nowSeconds) + TimeSpan.FromMicroseconds(nowMicroseconds);
            long left = timeLeft(TimeSpan.FromMilliseconds(timeoutMs), TimeSpan.FromMilliseconds(connectTimeoutMs), connecting, elapsed);
            checks.FailUnless(left == result, comment);
        }

        return Task.FromResult(checks.Result);
    }
}
