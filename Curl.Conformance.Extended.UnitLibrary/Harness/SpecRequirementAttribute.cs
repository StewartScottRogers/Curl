namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Marks a conformance test as covering one requirement of an extended protocol's
/// specification, named <c>&lt;Name&gt;:&lt;source&gt;-&lt;locator&gt;</c> (for example
/// <c>Mqtt5:MQTT-3.1.2-3</c> or <c>Coap:RFC7252-5.4.1-1</c>). A test may carry several.
/// The requirement-matrix check (<see cref="RequirementMatrix"/>) requires every carried ID to
/// be a row of <c>Documentation/Conformance/Extended/&lt;Name&gt;.md</c>.
/// </summary>
/// <param name="id">The requirement ID the test covers.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class SpecRequirementAttribute(string id) : Attribute
{
    /// <summary>Gets the requirement ID the test covers.</summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets the protocol name the ID starts with: the text before its first colon, or the
    /// whole ID when it has none.
    /// </summary>
    public string ProtocolName => ProtocolNameOf(Id);

    /// <summary>Returns the protocol name a requirement ID starts with.</summary>
    /// <param name="requirementId">A requirement ID.</param>
    /// <returns>The text before the first colon, or the whole ID when it has none.</returns>
    public static string ProtocolNameOf(string requirementId)
    {
        int colon = requirementId.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? requirementId : requirementId[..colon];
    }
}
