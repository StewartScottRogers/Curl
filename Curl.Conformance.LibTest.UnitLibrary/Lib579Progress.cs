using System.Globalization;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib579.c</c>'s progress state and callbacks: <c>started</c>, <c>last_ul</c> and <c>last_ul_total</c>, the
/// progress callback <see cref="Callback"/> and <c>progress_final_report</c> as <see cref="FinalReport"/>.
/// </summary>
/// <param name="path">The file in <c>libtest_arg2</c> the lines are appended to.</param>
/// <param name="error">The program's stderr, where a line goes when the file does not open.</param>
public sealed class Lib579Progress(string? path, TextWriter error)
{
    private readonly TextWriter error = error ?? throw new ArgumentNullException(nameof(error));
    private bool started;
    private long lastUpload;
    private long lastUploadTotal;

    /// <summary>
    /// <c>t579_progress_callback</c>: the end report when a started upload's count falls to zero after a non-zero
    /// one, then the counts recorded, then the start report on the first call.
    /// </summary>
    /// <param name="downloadTotal">Not used.</param>
    /// <param name="downloadNow">Not used.</param>
    /// <param name="uploadTotal">The upload total.</param>
    /// <param name="uploadNow">The bytes uploaded so far.</param>
    /// <returns>0, to go on.</returns>
    public int Callback(double downloadTotal, double downloadNow, double uploadTotal, double uploadNow)
    {
        if (started && uploadNow <= 0.0 && lastUpload != 0)
        {
            FinalReport();
        }

        lastUpload = (long)uploadNow;
        lastUploadTotal = (long)uploadTotal;
        if (!started)
        {
            Report("start");
            started = true;
        }

        return 0;
    }

    /// <summary><c>progress_final_report</c>: appends <c>Progress: end UL %zu/%zu</c> and clears <c>started</c>.</summary>
    public void FinalReport()
    {
        Report("end");
        started = false;
    }

    private void Report(string phase)
    {
        string line = string.Create(CultureInfo.InvariantCulture, $"Progress: {phase} UL {lastUpload}/{lastUploadTotal}\n");
        if (!LibTestArgumentFile.TryWrite(path, line, append: true))
        {
            error.Write(line);
            error.Write($"Progress: {phase} UL, cannot open {path}\n");
        }
    }
}
