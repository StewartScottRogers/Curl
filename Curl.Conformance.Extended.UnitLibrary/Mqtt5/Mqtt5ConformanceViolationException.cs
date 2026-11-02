namespace Curl.Conformance.Extended.Mqtt5;

/// <summary>
/// Raised by the MQTT 5 reference broker when the client breaks a statement of OASIS MQTT Version 5.0;
/// the harness rethrows it from <c>ExtendedConformanceHarness.RunAsync</c>, so the case fails naming it.
/// </summary>
/// <param name="requirementId">The requirement the client broke, such as <c>Mqtt5:MQTT-3.1.2-3</c>.</param>
/// <param name="detail">What the broker received.</param>
public sealed class Mqtt5ConformanceViolationException(string requirementId, string detail)
    : Exception($"{requirementId}: {detail}")
{
    /// <summary>Gets the requirement the client broke.</summary>
    public string RequirementId { get; } = requirementId;
}
