namespace Curl.Conformance.Extended.Harness;

/// <summary>One row of a requirement matrix: <c>| ID | Level | Requirement | Covered by |</c>.</summary>
/// <param name="Id">The requirement ID.</param>
/// <param name="Level">The requirement's level (MUST, SHOULD, MAY and so on).</param>
/// <param name="Requirement">The requirement's text.</param>
/// <param name="CoveredBy">The covering test as <c>Class.Method</c>, or <c>n/a: &lt;reason&gt;</c>.</param>
public sealed record RequirementMatrixRow(string Id, string Level, string Requirement, string CoveredBy);
