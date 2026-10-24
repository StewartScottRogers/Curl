namespace Curl.Mcp.Protocol.Server;

/// <summary>The <c>serverInfo</c> the server answers <c>initialize</c> with.</summary>
/// <param name="Name">The server's name, e.g. <c>Curl.Mcp</c>.</param>
/// <param name="Version">The server's version, e.g. the host assembly's version <c>0.1.0.0</c>.</param>
public sealed record McpServerInfo(string Name, string Version);
