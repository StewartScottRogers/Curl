namespace Curl.Conformance;

/// <summary>What <see cref="UpstreamTestConditionalLines"/> decided about one line of a test file.</summary>
internal enum UpstreamTestLineDisposition
{
    /// <summary>The line is output: it is outside every <c>%if</c> block, or in a branch that holds.</summary>
    Kept,

    /// <summary>The line is not output: it is a <c>%if</c>, <c>%else</c> or <c>%endif</c> line, or in a branch that does not hold.</summary>
    Dropped,

    /// <summary>The line is a <c>%else</c> or <c>%endif</c> with no <c>%if</c>; upstream stops preprocessing there.</summary>
    Stopped,
}
