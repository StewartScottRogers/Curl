namespace Curl.Extension.OsCredentialStore.UnitTests;

/// <summary>Answers every command with one recorded output and keeps the command it was asked to run.</summary>
/// <param name="output">The recorded output.</param>
internal sealed class RecordedCommandRunner(CommandOutput output) : ICommandRunner
{
    public string? FileName { get; private set; }

    public IReadOnlyList<string> Arguments { get; private set; } = [];

    public ValueTask<CommandOutput> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        FileName = fileName;
        Arguments = arguments;
        return ValueTask.FromResult(output);
    }
}
