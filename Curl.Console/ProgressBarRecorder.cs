using System.Globalization;
using System.Text;

namespace Curl.Console;

/// <summary>
/// Draws curl 8.21.0's <c>-#</c>/<c>--progress-bar</c> bar from one transfer's progress
/// reports, on the clock it is given, as <c>tool_progress_cb</c> in curl's
/// <c>src/tool_cb_prg.c</c> draws it (task BL-132).
/// </summary>
/// <remarks>
/// Each report stands for one call of curl's progress callback. While the expected total is
/// known, a call draws <c>\r</c>, the bar of <c>#</c> padded to the width less seven, a space
/// and the percentage as <c>%5.1f%%</c>, when the position moved, and at most every 100 ms
/// before it reaches the total; while it is not, a call made 100 ms or more after the last
/// draws <c>fly</c>'s <c>-=O=-</c> animation. The <c>-C</c> offset counts towards both the
/// position and the total, as curl's <c>initial_size</c> does. The newline curl writes after
/// the bar is the runner's to write, once the transfer's failure lines are out, when
/// <see cref="HasBeenCalled" /> says the callback ran.
/// </remarks>
internal sealed class ProgressBarRecorder
{
    /// <summary>The widest bar curl draws (<c>MAX_BARLENGTH</c>).</summary>
    internal const int MaximumWidth = 400;

    /// <summary>The narrowest bar curl draws (<c>MIN_BARLENGTH</c>).</summary>
    internal const int MinimumWidth = 20;

    /// <summary>How many milliseconds curl waits between draws that do not reach the total.</summary>
    private const int RedrawMilliseconds = 100;

    /// <summary>The sine table <c>fly</c> places its four <c>#</c> by, copied from curl 8.21.0.</summary>
    private static readonly int[] Sinus =
    [
        515704, 531394, 547052, 562664, 578214, 593687, 609068, 624341, 639491, 654504,
        669364, 684057, 698568, 712883, 726989, 740870, 754513, 767906, 781034, 793885,
        806445, 818704, 830647, 842265, 853545, 864476, 875047, 885248, 895069, 904500,
        913532, 922156, 930363, 938145, 945495, 952406, 958870, 964881, 970434, 975522,
        980141, 984286, 987954, 991139, 993840, 996054, 997778, 999011, 999752, 999999,
        999754, 999014, 997783, 996060, 993848, 991148, 987964, 984298, 980154, 975536,
        970449, 964898, 958888, 952426, 945516, 938168, 930386, 922180, 913558, 904527,
        895097, 885277, 875077, 864507, 853577, 842299, 830682, 818739, 806482, 793922,
        781072, 767945, 754553, 740910, 727030, 712925, 698610, 684100, 669407, 654548,
        639536, 624386, 609113, 593733, 578260, 562710, 547098, 531440, 515751, 500046,
        484341, 468651, 452993, 437381, 421830, 406357, 390976, 375703, 360552, 345539,
        330679, 315985, 301474, 287158, 273052, 259170, 245525, 232132, 219003, 206152,
        193590, 181331, 169386, 157768, 146487, 135555, 124983, 114781, 104959, 95526,
        86493, 77868, 69660, 61876, 54525, 47613, 41147, 35135, 29581, 24491,
        19871, 15724, 12056, 8868, 6166, 3951, 2225, 990, 248, 0,
        244, 982, 2212, 3933, 6144, 8842, 12025, 15690, 19832, 24448,
        29534, 35084, 41092, 47554, 54462, 61809, 69589, 77794, 86415, 95445,
        104873, 114692, 124891, 135460, 146389, 157667, 169282, 181224, 193480, 206039,
        218888, 232015, 245406, 259048, 272928, 287032, 301346, 315856, 330548, 345407,
        360419, 375568, 390841, 406221, 421693, 437243, 452854, 468513, 484202, 499907,
    ];

    private readonly TimeProvider timeProvider;

    private readonly long initialSize;

    private readonly int width;

    private readonly StringBuilder drawn = new();

    private long downloaded;

    private long downloadTotal;

    private long uploaded;

    private long uploadTotal;

    private bool bytesReported;

    private int calls;

    private long previousPoint;

    private long previousTime;

    private int tick = 150;

    private int flyPosition;

    private int flyMove = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressBarRecorder" /> class, as curl's
    /// <c>progressbarinit</c> does.
    /// </summary>
    /// <param name="timeProvider">The clock every call reads.</param>
    /// <param name="initialSize">The <c>-C</c> offset the transfer resumes from, or 0.</param>
    /// <param name="terminalColumns">The terminal width, from <see cref="TerminalColumns" />.</param>
    internal ProgressBarRecorder(TimeProvider timeProvider, long initialSize, int terminalColumns)
    {
        this.timeProvider = timeProvider;
        this.initialSize = initialSize;
        width = Width(terminalColumns);
    }

    /// <summary>Gets a value indicating whether curl's progress callback ran, so the bar ends with a newline.</summary>
    internal bool HasBeenCalled => calls > 0;

    /// <summary>Gets everything drawn so far, each draw starting with a carriage return and none ending in a newline.</summary>
    internal string Drawn => drawn.ToString();

    /// <summary>
    /// The bar width curl's <c>update_width</c> takes from the terminal width: from
    /// <see cref="MinimumWidth" /> to <see cref="MaximumWidth" />, and <see cref="MinimumWidth" />
    /// for 20 columns or fewer.
    /// </summary>
    /// <param name="terminalColumns">The terminal width.</param>
    /// <returns>The bar width.</returns>
    internal static int Width(int terminalColumns) =>
        terminalColumns > MinimumWidth ? Math.Min(terminalColumns, MaximumWidth) : MinimumWidth;

    /// <summary>Calls the callback with no bytes, as curl first does once the transfer is under way.</summary>
    internal void ReportTransferStarted() => Call();

    /// <summary>Calls the callback with the bytes received so far.</summary>
    /// <param name="bytesSoFar">The body bytes received.</param>
    /// <param name="expectedTotal">The body size expected, or <see langword="null" /> when unknown (curl's 0).</param>
    internal void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
        downloaded = bytesSoFar;
        downloadTotal = expectedTotal ?? 0;
        bytesReported = true;
        Call();
    }

    /// <summary>Calls the callback with the bytes sent so far.</summary>
    /// <param name="bytesSoFar">The body bytes sent.</param>
    /// <param name="expectedTotal">The upload size, or <see langword="null" /> when unknown (curl's 0).</param>
    internal void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        uploaded = bytesSoFar;
        uploadTotal = expectedTotal ?? 0;
        bytesReported = true;
        Call();
    }

    /// <summary>
    /// Makes the last call of a successful transfer whose handler reported no bytes, as
    /// <c>file://</c>'s does not: curl's <c>file://</c> transfer ends with a call that has its
    /// size as both the bytes received and the total, so a non-empty file ends on a full bar.
    /// </summary>
    /// <param name="succeeded">Whether the transfer succeeded.</param>
    /// <param name="bytesTransferred">The body bytes the transfer moved.</param>
    internal void Finish(bool succeeded, long bytesTransferred)
    {
        if (succeeded && !bytesReported)
        {
            ReportDownloaded(bytesTransferred, bytesTransferred);
        }
    }

    /// <summary>One call of curl's <c>tool_progress_cb</c>.</summary>
    private void Call()
    {
        long now = timeProvider.GetTimestamp();
        long total = CappedSum(initialSize, CappedSum(downloadTotal, uploadTotal));
        long point = CappedSum(initialSize, CappedSum(downloaded, uploaded));
        if (calls > 0 && !TakesCall(now, total, point))
        {
            return;
        }

        calls++;
        if (total > 0 && point != previousPoint)
        {
            drawn.Append(Bar(point, total));
        }

        previousPoint = point;
        previousTime = now;
    }

    /// <summary>
    /// Tells whether a call after the first goes on to draw: with a known total, when the
    /// position moved and either reached the total or 100 ms have passed; with none, when
    /// 100 ms have passed, after drawing <c>fly</c>'s animation.
    /// </summary>
    /// <param name="now">The clock's timestamp.</param>
    /// <param name="total">The expected total, 0 when unknown.</param>
    /// <param name="point">The position.</param>
    /// <returns><see langword="false" /> when curl returns without drawing.</returns>
    private bool TakesCall(long now, long total, long point)
    {
        bool waited = timeProvider.GetElapsedTime(previousTime, now).TotalMilliseconds >= RedrawMilliseconds;
        if (total > 0)
        {
            return point != previousPoint && (waited || point >= total);
        }

        if (waited)
        {
            Fly(point != previousPoint);
        }

        return waited;
    }

    /// <summary>
    /// Formats the bar as curl's <c>"\r%-Ns %5.1f%%"</c>, N being the width less seven.
    /// </summary>
    /// <param name="point">The position.</param>
    /// <param name="total">The expected total; the position when past it.</param>
    /// <returns>The bar, starting with a carriage return.</returns>
    private string Bar(long point, long total)
    {
        double fraction = (double)point / Math.Max(total, point);
        int barWidth = width - 7;
        int hashes = (int)(barWidth * fraction);
        string percent = (fraction * 100.0).ToString("F1", CultureInfo.InvariantCulture);

        return "\r" + new string('#', hashes).PadRight(barWidth) + " " + percent.PadLeft(5) + "%";
    }

    /// <summary>
    /// Draws one frame of the animation curl's <c>fly</c> draws while the total is unknown:
    /// <c>-=O=-</c> sliding along the line, bouncing at each end, crossed by four <c>#</c> on
    /// a sine wave.
    /// </summary>
    /// <param name="moved">Whether the position moved since the last call, which moves the <c>-=O=-</c>.</param>
    private void Fly(bool moved)
    {
        char[] line = new string(' ', width).ToCharArray();
        "-=O=-".CopyTo(0, line, flyPosition, 5);
        int step = 1000000 / (width - 2);
        for (int offset = 0; offset < 20; offset += 5)
        {
            line[Sinus[(tick + offset) % 200] / step] = '#';
        }

        drawn.Append('\r').Append(line);
        tick = (tick + 2) % 200;
        flyPosition += moved ? flyMove : 0;
        if (flyPosition >= width - 6)
        {
            flyMove = -1;
            flyPosition = width - 6;
        }
        else if (flyPosition < 0)
        {
            flyMove = 1;
            flyPosition = 0;
        }
    }

    /// <summary>Adds two sizes, capped at <see cref="long.MaxValue" /> as curl caps them.</summary>
    /// <param name="first">The first size.</param>
    /// <param name="second">The second size.</param>
    /// <returns>The sum.</returns>
    private static long CappedSum(long first, long second) =>
        long.MaxValue - first < second ? long.MaxValue : first + second;
}
