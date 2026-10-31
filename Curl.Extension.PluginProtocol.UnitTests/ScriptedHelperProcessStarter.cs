namespace Curl.Extension.PluginProtocol;

/// <summary>Gives one scripted helper, or fails to start one, and records what it was asked to start.</summary>
/// <param name="helper">The helper to give, or <see langword="null" /> to fail as a missing program does.</param>
internal sealed class ScriptedHelperProcessStarter(IHelperProcess? helper) : IHelperProcessStarter
{
    public string? StartedProgram { get; private set; }

    public string? StartedUrl { get; private set; }

    public IHelperProcess Start(string program, string url)
    {
        StartedProgram = program;
        StartedUrl = url;
        return helper ?? throw new IOException("The system cannot find the file specified.");
    }
}
