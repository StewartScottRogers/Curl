using Curl.Extension.Protocol.McpClient.Stdio;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>A fake <see cref="IMcpServerProcessStarter"/> that records the command and gives a set process, or throws a set exception.</summary>
internal sealed class ScriptedMcpServerProcessStarter(IMcpServerProcess? process, Exception? failure = null) : IMcpServerProcessStarter
{
    public string? Command { get; private set; }

    public IReadOnlyList<string>? Arguments { get; private set; }

    public IMcpServerProcess Start(string command, IReadOnlyList<string> arguments)
    {
        Command = command;
        Arguments = arguments;
        return process ?? throw failure!;
    }
}
