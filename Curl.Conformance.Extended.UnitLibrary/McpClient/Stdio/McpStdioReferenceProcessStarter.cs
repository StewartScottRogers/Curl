using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.McpClient.Stdio;

/// <summary>
/// The stand-in process starter the conformance harness hands curl for <c>mcp+stdio://</c> (BL-4483): every program it
/// is asked to start is a <see cref="McpStdioReferenceProcess" /> serving <paramref name="server" />, and it reads no
/// environment variable, so no real process starts and no <c>PATH</c> is searched.
/// </summary>
/// <param name="server">The JSON-RPC side every started server answers with.</param>
/// <param name="behaviour">How every started server misbehaves, if at all.</param>
public sealed class McpStdioReferenceProcessStarter(McpReferenceToolServer server, McpStdioServerBehaviour behaviour) : IExtensionProcessStarter
{
    private readonly List<McpStdioReferenceProcess> started = [];

    /// <summary>Gets every server started, in order.</summary>
    public IReadOnlyList<McpStdioReferenceProcess> Started => started;

    /// <inheritdoc />
    public string? ReadEnvironmentVariable(string name) => null;

    /// <inheritdoc />
    public IExtensionProcess Start(string program, IReadOnlyList<string> arguments)
    {
        McpStdioReferenceProcess process = new(server, behaviour, program, arguments);
        started.Add(process);
        return process;
    }
}
