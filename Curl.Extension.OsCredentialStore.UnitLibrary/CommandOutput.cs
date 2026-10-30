namespace Curl.Extension.OsCredentialStore;

/// <summary>What a credential store's command-line tool printed and the code it exited with.</summary>
/// <param name="ExitCode">The tool's exit code; 127 when it could not be started.</param>
/// <param name="StandardOutput">What it wrote to standard output, decoded as UTF-8.</param>
/// <param name="StandardError">What it wrote to standard error, decoded as UTF-8.</param>
internal sealed record CommandOutput(int ExitCode, string StandardOutput, string StandardError);
