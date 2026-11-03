namespace Curl.Conformance.Extended.Harness;

/// <summary>One conformance test and the requirement IDs its <see cref="SpecRequirementAttribute"/>s carry.</summary>
/// <param name="TestName">The test as <c>Class.Method</c>.</param>
/// <param name="RequirementIds">The requirement IDs the test carries.</param>
public sealed record SpecRequirementCarrier(string TestName, IReadOnlyList<string> RequirementIds);
