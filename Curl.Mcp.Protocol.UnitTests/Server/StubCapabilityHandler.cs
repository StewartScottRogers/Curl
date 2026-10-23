using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>A method handler that brings the capability a test names, as a <c>tools/list</c> or <c>resources/list</c> handler would.</summary>
internal sealed class StubCapabilityHandler(string method, string capabilityName, string capabilityJson)
    : StubMcpMethodHandler(method, (_, _) => Task.FromResult(JsonDocument.Parse("{}").RootElement)), IMcpCapabilityContributor
{
    public string CapabilityName => capabilityName;

    public JsonElement Capability => JsonDocument.Parse(capabilityJson).RootElement.Clone();
}
