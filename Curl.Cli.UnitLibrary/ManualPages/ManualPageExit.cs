namespace Curl.Cli.ManualPages;

/// <summary>Thrown where upstream's <c>scripts/managen</c> calls <c>exit</c> or <c>die</c>, carrying the exit code.</summary>
public sealed class ManualPageExit : Exception
{
    /// <summary>Creates the exit with its code.</summary>
    /// <param name="exitCode">The code the script exits with.</param>
    public ManualPageExit(int exitCode) => ExitCode = exitCode;

    /// <summary>The code the script exits with.</summary>
    public int ExitCode { get; }
}
