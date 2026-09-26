namespace Curl.Conformance;

/// <summary>
/// Resolves the <c>%if &lt;feature&gt;</c>, <c>%if !&lt;feature&gt;</c>, <c>%else</c> and
/// <c>%endif</c> lines of a test file line by line, nesting included, exactly as
/// <c>runtests.pl</c>'s <c>prepro</c> does at <c>curl-8_21_0</c>.
/// </summary>
/// <remarks>
/// Each directive may be preceded by spaces. <c>%if</c> needs a space after it and reads the
/// feature name from the characters <c>A-Z a-z 0-9 ! _ -</c>, ignoring the rest of the line;
/// one leading <c>!</c> negates it. <c>%else</c> and <c>%endif</c> match as prefixes, as
/// upstream's regular expressions do. A <c>%else</c> or <c>%endif</c> with no open <c>%if</c>
/// stops preprocessing, and an <c>%if</c> still open at the end of the file is not an error.
/// </remarks>
internal sealed class UpstreamTestConditionalLines(IReadOnlySet<string> features)
{
    private const string FeatureNameCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!_-";

    private readonly Stack<bool> enclosingShow = new();
    private readonly Stack<bool> alternativeShow = new();
    private bool show = true;

    /// <summary>
    /// A sentence naming the stray <c>%else</c> or <c>%endif</c> that stopped preprocessing,
    /// or <see langword="null"/>.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>Reads one line, updating the open blocks.</summary>
    /// <param name="line">The line, with its line feed.</param>
    /// <param name="lineNumber">Its 1-based number, for <see cref="Error"/>.</param>
    /// <returns>Whether the line is output, dropped, or stops preprocessing.</returns>
    public UpstreamTestLineDisposition ReadLine(string line, int lineNumber)
    {
        string directive = line.TrimStart(' ');
        if (directive.StartsWith("%if ", StringComparison.Ordinal))
        {
            OpenIf(directive[4..]);
            return UpstreamTestLineDisposition.Dropped;
        }

        if (directive.StartsWith("%else", StringComparison.Ordinal))
        {
            return Else(lineNumber);
        }

        if (directive.StartsWith("%endif", StringComparison.Ordinal))
        {
            return EndIf(lineNumber);
        }

        return show ? UpstreamTestLineDisposition.Kept : UpstreamTestLineDisposition.Dropped;
    }

    private void OpenIf(string afterIf)
    {
        string condition = new(afterIf.TakeWhile(FeatureNameCharacters.Contains).ToArray());
        bool negated = condition.StartsWith('!');
        bool holds = features.Contains(negated ? condition[1..] : condition) != negated;
        enclosingShow.Push(show);
        alternativeShow.Push(show && !holds);
        show = show && holds;
    }

    private UpstreamTestLineDisposition Else(int lineNumber)
    {
        if (alternativeShow.Count == 0)
        {
            return Stop($"%else on line {lineNumber} has no %if.");
        }

        show = alternativeShow.Peek();
        return UpstreamTestLineDisposition.Dropped;
    }

    private UpstreamTestLineDisposition EndIf(int lineNumber)
    {
        if (enclosingShow.Count == 0)
        {
            return Stop($"%endif on line {lineNumber} has no %if.");
        }

        show = enclosingShow.Pop();
        alternativeShow.Pop();
        return UpstreamTestLineDisposition.Dropped;
    }

    private UpstreamTestLineDisposition Stop(string error)
    {
        Error = error;
        return UpstreamTestLineDisposition.Stopped;
    }
}
