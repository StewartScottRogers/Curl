namespace Curl.Conformance.Differential;

/// <summary>How Curl's run of a case compared with the reference curl's.</summary>
/// <param name="Case">The case.</param>
/// <param name="Differences">Each difference as its report block (<c>exit: ...</c>, <c>stdout:...</c> and so on); empty when the runs agree.</param>
/// <param name="OursElapsed">How long Curl took.</param>
/// <param name="RealElapsed">How long the reference curl took.</param>
public sealed record DifferentialCaseOutcome(
    DifferentialCase Case,
    IReadOnlyList<string> Differences,
    TimeSpan OursElapsed,
    TimeSpan RealElapsed)
{
    /// <summary>Gets whether Curl took more than three times the reference's time plus 1.5 seconds.</summary>
    public bool IsSlow => OursElapsed.TotalSeconds > (3 * RealElapsed.TotalSeconds) + 1.5;
}
