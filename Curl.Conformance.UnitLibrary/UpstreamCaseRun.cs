namespace Curl.Conformance;

/// <summary>What one run of a test case produced, for <see cref="UpstreamCaseVerification"/>.</summary>
/// <param name="exitCode">curl's exit code.</param>
/// <param name="standardOutput">Every byte written to standard output.</param>
/// <param name="standardError">Every byte written to standard error.</param>
/// <param name="receivedBytes">Every byte the emulated server received.</param>
/// <param name="outputFileBytes">The <c>--output</c> file the harness added, empty when it was not written.</param>
internal sealed class UpstreamCaseRun(int exitCode, byte[] standardOutput, byte[] standardError, byte[] receivedBytes, byte[] outputFileBytes)
{
    /// <summary>curl's exit code.</summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>Every byte written to standard output.</summary>
    public byte[] StandardOutput { get; } = standardOutput;

    /// <summary>Every byte written to standard error.</summary>
    public byte[] StandardError { get; } = standardError;

    /// <summary>Every byte the emulated server received.</summary>
    public byte[] ReceivedBytes { get; } = receivedBytes;

    /// <summary>The <c>--output</c> file the harness added, empty when it was not written.</summary>
    public byte[] OutputFileBytes { get; } = outputFileBytes;
}
