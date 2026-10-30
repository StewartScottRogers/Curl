namespace Curl.Extension.OsCredentialStore;

/// <summary>Runs a credential store's command-line tool (<c>security</c>, <c>secret-tool</c>) to its end.</summary>
internal interface ICommandRunner
{
    /// <summary>Runs <paramref name="fileName" /> with <paramref name="arguments" /> and waits for it to exit.</summary>
    /// <param name="fileName">The tool.</param>
    /// <param name="arguments">Its arguments, each passed as one.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>What the tool printed and its exit code.</returns>
    ValueTask<CommandOutput> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
