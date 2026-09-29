namespace Curl.Output;

/// <summary>
/// The figures one status line of a <c>-Z</c> run's progress meter shows
/// (<see cref="ParallelProgressMeterText.StatusLine" />), as curl 8.21.0's <c>progress_meter</c>
/// computes them across every transfer of the run.
/// </summary>
/// <param name="DownloadPercent">The share of the known download size received, or <see langword="null" /> when unknown.</param>
/// <param name="UploadPercent">The share of the known upload size sent, or <see langword="null" /> when unknown.</param>
/// <param name="Downloaded">The bytes received so far.</param>
/// <param name="Uploaded">The bytes sent so far.</param>
/// <param name="Transfers">The transfers the run has taken on.</param>
/// <param name="Live">The transfers running.</param>
/// <param name="TotalSeconds">The expected total time in seconds; zero when unknown.</param>
/// <param name="SpentSeconds">The seconds since the run started.</param>
/// <param name="LeftSeconds">The expected seconds left; zero when unknown.</param>
/// <param name="Speed">The current speed in bytes per second, the higher of the two directions.</param>
public readonly record struct ParallelProgressFigures(
    long? DownloadPercent,
    long? UploadPercent,
    long Downloaded,
    long Uploaded,
    long Transfers,
    long Live,
    long TotalSeconds,
    long SpentSeconds,
    long LeftSeconds,
    long Speed);
