using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>
/// Implemented by a method handler that brings a server capability, so the <c>initialize</c> result
/// lists the capabilities of exactly the handlers the server was built with.
/// </summary>
public interface IMcpCapabilityContributor
{
    /// <summary>Gets the capability's member name in the <c>capabilities</c> object, e.g. <c>tools</c>.</summary>
    string CapabilityName { get; }

    /// <summary>Gets the capability's value, e.g. <c>{"listChanged":true}</c>.</summary>
    JsonElement Capability { get; }
}
