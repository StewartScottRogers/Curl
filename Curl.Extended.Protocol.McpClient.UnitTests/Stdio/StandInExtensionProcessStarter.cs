using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>A host's process starter that gives one stand-in process, or fails as a missing program does, and records what it was asked.</summary>
/// <param name="process">The process to give, or <see langword="null" /> to throw an <see cref="IOException" />.</param>
/// <param name="environment">The environment it reads variables from.</param>
internal sealed class StandInExtensionProcessStarter(IExtensionProcess? process, IReadOnlyDictionary<string, string?>? environment = null) : IExtensionProcessStarter
{
    private readonly List<string> readVariables = [];

    public string? StartedProgram { get; private set; }

    public IReadOnlyList<string>? StartedArguments { get; private set; }

    public IReadOnlyList<string> ReadVariables => readVariables;

    public string? ReadEnvironmentVariable(string name)
    {
        readVariables.Add(name);
        return environment?.GetValueOrDefault(name);
    }

    public IExtensionProcess Start(string program, IReadOnlyList<string> arguments)
    {
        StartedProgram = program;
        StartedArguments = arguments;
        return process ?? throw new IOException("The system cannot find the file specified.");
    }
}
