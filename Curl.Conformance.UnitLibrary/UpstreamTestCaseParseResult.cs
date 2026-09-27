using System.Diagnostics.CodeAnalysis;

namespace Curl.Conformance;

/// <summary>
/// The outcome of <see cref="UpstreamTestCaseParser.Parse"/>: either the
/// <see cref="UpstreamTestCase"/> or the <see cref="UpstreamTestCaseParseFailure"/> that stopped it.
/// </summary>
public sealed class UpstreamTestCaseParseResult
{
    private UpstreamTestCaseParseResult(UpstreamTestCase? testCase, UpstreamTestCaseParseFailure? failure)
    {
        TestCase = testCase;
        Failure = failure;
    }

    /// <summary>
    /// <see langword="true"/> when the file parsed and <see cref="TestCase"/> is set;
    /// <see langword="false"/> when <see cref="Failure"/> is set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(TestCase))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsParsed => TestCase is not null;

    /// <summary>The parsed test case; <see langword="null"/> when parsing failed.</summary>
    public UpstreamTestCase? TestCase { get; }

    /// <summary>The failure; <see langword="null"/> when the file parsed.</summary>
    public UpstreamTestCaseParseFailure? Failure { get; }

    internal static UpstreamTestCaseParseResult Parsed(UpstreamTestCase testCase) => new(testCase, null);

    internal static UpstreamTestCaseParseResult Failed(UpstreamTestCaseParseFailure failure) => new(null, failure);
}
