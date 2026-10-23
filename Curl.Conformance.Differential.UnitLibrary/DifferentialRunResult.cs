namespace Curl.Conformance.Differential;

/// <summary>What one binary did with one case.</summary>
/// <param name="ExitCode">The process exit code, or <see langword="null"/> when it timed out.</param>
/// <param name="StandardOutput">The bytes written to standard output.</param>
/// <param name="StandardError">The bytes written to standard error.</param>
/// <param name="Files">The files the run created or changed in its working directory, by relative path.</param>
/// <param name="Wire">The bytes the loopback server received during the run.</param>
/// <param name="Elapsed">How long the run took.</param>
public sealed record DifferentialRunResult(
    int? ExitCode,
    byte[] StandardOutput,
    byte[] StandardError,
    IReadOnlyDictionary<string, byte[]> Files,
    byte[] Wire,
    TimeSpan Elapsed);
