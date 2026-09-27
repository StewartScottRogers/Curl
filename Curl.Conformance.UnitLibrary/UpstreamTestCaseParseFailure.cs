namespace Curl.Conformance;

/// <summary>Why <see cref="UpstreamTestCaseParser"/> could not parse a test file, and where.</summary>
public sealed class UpstreamTestCaseParseFailure
{
    /// <summary>Creates a failure.</summary>
    /// <param name="section">The section path the failure is in, such as <c>&lt;reply&gt;&lt;data&gt;</c>.</param>
    /// <param name="lineNumber">The 1-based line the failure points at.</param>
    /// <param name="message">A sentence naming the section, the line and what is wrong.</param>
    public UpstreamTestCaseParseFailure(string section, int lineNumber, string message)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(message);
        Section = section;
        LineNumber = lineNumber;
        Message = message;
    }

    /// <summary>
    /// The section path the failure is in, such as <c>&lt;reply&gt;&lt;data&gt;</c>, or the
    /// closing tag at fault, such as <c>&lt;/verify&gt;</c>.
    /// </summary>
    public string Section { get; }

    /// <summary>The 1-based line the failure points at.</summary>
    public int LineNumber { get; }

    /// <summary>A sentence naming the section, the line and what is wrong.</summary>
    public string Message { get; }
}
