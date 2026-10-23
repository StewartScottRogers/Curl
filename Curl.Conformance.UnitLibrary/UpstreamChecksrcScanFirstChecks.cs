using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// The per-line checks of <c>checksrc.pl</c>'s <c>scanfile</c> from script line 532 to 900 (curl-8_22_0, BL-3417), in
/// the script's order: the copyright years, the checks on the line with its comments, the C89 comment stripping, the
/// preprocessor line checks and the code checks up to <c>sizeof</c>. A preprocessor line (or one continuing a
/// preprocessor line) leaves <see cref="UpstreamChecksrcRun.FileScan.Preprocessor"/> set and skips the code checks, as
/// the script's <c>goto preproc</c> does. Perl's <c>\w</c>, <c>\W</c> and <c>\s</c> on a byte string match ASCII only,
/// so the patterns spell them out.
/// </summary>
internal static class UpstreamChecksrcScanFirstChecks
{
    private const string Word = "[A-Za-z0-9_]";
    private const string NotWord = "[^A-Za-z0-9_]";
    private const string Space = @"[ \t\n\r\f\v]";

    private static readonly Regex CopyrightStatement = Pattern(@"\* +copyright .* (\d\d\d\d|)", RegexOptions.IgnoreCase);
    private static readonly Regex Year = Pattern(@"[0-9]{4}");
    private static readonly Regex HttpsLink = Pattern(" https://");
    private static readonly Regex Tab = Pattern("^(.*)\t");
    private static readonly Regex TrailingSpace = Pattern(@"^(.*)[ \t]+\z");
    private static readonly Regex CommentStartNoSpace = Pattern($@"^(.*)/\*{Word}");
    private static readonly Regex CommentEndNoSpace = Pattern($@"^(.*){Word}\*/");
    private static readonly Regex Fixme = Pattern("(.*)(FIXME|TODO)");
    private static readonly Regex FullComment = Pattern(@"/\*.*\*/");
    private static readonly Regex CommentStart = Pattern(@"/\*.*");
    private static readonly Regex CommentEnd = Pattern(@".*\*/");
    private static readonly Regex BackslashEnd = Pattern(@"\\ *\z");
    private static readonly Regex CppComment = Pattern("^(([^\"\\*]*)[^:\"]|)//");
    private static readonly Regex IfDefinedSingle = Pattern($@"^({Space}*#{Space}*if{Space}+!?{Space}*defined\()[a-zA-Z0-9_]+\)$");
    private static readonly Regex Include = Pattern($"^(#{Space}*include{Space}+)([\">].*[>}}\"])");
    private static readonly Regex SpaceBeforeHash = Pattern(@"^([ \t]+)#");
    private static readonly Regex PreprocessorDirective = Pattern(@"^[ \t]*#");
    private static readonly Regex QuotedString = Pattern("\".*\"");
    private static readonly Regex SpaceBeforeParen = Pattern(@"^(.*)(for|if|while|switch| ([a-zA-Z0-9_]+)) \((.)");
    private static readonly Regex ControlWord = Pattern("(for|if|while|switch)");
    private static readonly Regex EqualsNull = Pattern(@"^(.*)(if|while)(\(.*?)([!=]= NULL|NULL [!=]=)");
    private static readonly Regex NotEqualsZero = Pattern(@"^(.*)(if|while)(\(.*[^)]) != 0[^x]");
    private static readonly Regex DoBrace = Pattern(@"^( *)do( *)\{");
    private static readonly Regex BraceWhile = Pattern(@"^( *)\}( *)while");
    private static readonly Regex IfCondition = Pattern($@"^((.*{Space})(if) *\()(.*)\)(.*)");
    private static readonly Regex BlankRest = Pattern("^ *$");
    private static readonly Regex ContinuedCondition = Pattern(@"^ *[,{&|\\]+");
    private static readonly Regex SpaceAfterParen = Pattern(@"^(.*[a-z])\( ", RegexOptions.IgnoreCase);
    private static readonly Regex AndNoSpaceBefore = Pattern($"^(.*){Word}&&", RegexOptions.IgnoreCase);
    private static readonly Regex AndNoSpaceAfter = Pattern($"^(.*&&){Word}", RegexOptions.IgnoreCase);
    private static readonly Regex ColonNoSpaceBefore = Pattern($@"^(.*[^']\?[^'].*)({Word}|\)|\]|')\:", RegexOptions.IgnoreCase);
    private static readonly Regex ColonNoSpaceAfter = Pattern($@"^(.*[^'""]\?[^'""].*)\:({Word}|\)|\]|')", RegexOptions.IgnoreCase);
    private static readonly Regex QuotedColonPair = Pattern("'(.)':'(.)'");
    private static readonly Regex QuestionNoSpaceBefore = Pattern($@"^(.*)({Word}|\)|\]|')\?", RegexOptions.IgnoreCase);
    private static readonly Regex QuotedQuestion = Pattern("'?'");
    private static readonly Regex QuestionNoSpaceAfter = Pattern($@"^(.*)\?{Word}", RegexOptions.IgnoreCase);
    private static readonly Regex ThanNoSpaceBefore = Pattern($@"^(.*)({Word}|\)|\])[<>]");
    private static readonly Regex ThanNoSpaceAfter = Pattern($@"^(.*)[^-][<>]({Word}|\(|\[)");
    private static readonly Regex SpaceBeforeClose = Pattern(@"(.*[^\) ]) \)");
    private static readonly Regex SpaceBeforeComma = Pattern("(.*[^ ]) ,");
    private static readonly Regex ReturnNoSpace = Pattern($@"^(.*{NotWord})return\(");
    private static readonly Regex ReturnParen = Pattern($@"^(.*{NotWord})return \({Word}*\);");
    private static readonly Regex SizeofNoParen = Pattern(@"^(.*)sizeof *([ (])");

    /// <summary>Runs the checks on the scan's current line, warning through <paramref name="run"/>.</summary>
    /// <param name="run">The run that counts and prints the warnings.</param>
    /// <param name="scan">The file being scanned, its current line in <see cref="UpstreamChecksrcRun.FileScan.Text"/>.</param>
    public static void Check(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(scan);
        RecordCopyright(scan);
        CheckLineWithComments(run, scan);
        StripComments(scan);
        if (IsPreprocessorLine(run, scan))
        {
            return;
        }

        string noStrings = QuotedString.Replace(scan.Text, "");
        CheckSpaceBeforeParen(run, scan, noStrings);
        CheckComparisons(run, scan, noStrings);
        CheckCondition(run, scan, noStrings);
        CheckOperators(run, scan, noStrings);
        CheckColon(run, scan, noStrings, ColonNoSpaceBefore, "missing space before colon");
        CheckColon(run, scan, noStrings, ColonNoSpaceAfter, "missing space after colon");
        CheckQuestionAndThan(run, scan, noStrings);
        CheckParensAndReturn(run, scan);
    }

    private static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options | RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static void Warn(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string name, int column, string message, bool isError = false) =>
        run.Warn(name, scan.Line, column, scan.File, scan.Text, message, isError);

    // A year-less statement records year -1 at column 0: Perl's $1 is then the empty match of the statement pattern.
    private static void RecordCopyright(UpstreamChecksrcRun.FileScan scan)
    {
        string line = scan.Text;
        if (!CopyrightStatement.IsMatch(line))
        {
            return;
        }

        MatchCollection years = Year.Matches(line);
        foreach (Match year in years)
        {
            scan.Copyright.Add(new(int.Parse(year.Value, System.Globalization.CultureInfo.InvariantCulture), scan.Line, line.IndexOf(year.Value, StringComparison.Ordinal), line));
        }

        if (years.Count == 0)
        {
            scan.Copyright.Add(new(-1, scan.Line, 0, line));
        }
    }

    private static void CheckLineWithComments(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        string line = scan.Text;
        if (line.Length > run.MaxColumn && !HttpsLink.IsMatch(line))
        {
            Warn(run, scan, "LONGLINE", line.Length, $"Longer than {run.MaxColumn} columns");
        }

        WarnAtGroup(run, scan, Tab, "TABS", 0, "Contains TAB character", true);
        WarnAtGroup(run, scan, TrailingSpace, "TRAILINGSPACE", 0, "Trailing whitespace");
        WarnAtGroup(run, scan, CommentStartNoSpace, "COMMENTNOSPACESTART", 2, "Missing space after comment start");
        WarnAtGroup(run, scan, CommentEndNoSpace, "COMMENTNOSPACEEND", 1, "Missing space before comment end");
        Match fixme = Fixme.Match(line);
        if (fixme.Success)
        {
            Warn(run, scan, "FIXME", fixme.Groups[1].Length, $"Avoid {fixme.Groups[2].Value} comments. Add to documentation instead");
        }
    }

    // A warning at the first group's length plus an offset, when the pattern matches the current line.
    private static void WarnAtGroup(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, Regex pattern, string name, int offset, string message, bool isError = false) =>
        WarnAtGroup(run, scan, pattern, scan.Text, name, offset, message, isError);

    private static void WarnAtGroup(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, Regex pattern, string text, string name, int offset, string message, bool isError = false)
    {
        Match match = pattern.Match(text);
        if (match.Success)
        {
            Warn(run, scan, name, match.Groups[1].Length + offset, message, isError);
        }
    }

    // The script's "comment:" label: a closed comment's end loops back to strip what follows it.
    private static void StripComments(UpstreamChecksrcRun.FileScan scan)
    {
        while (scan.InComment)
        {
            if (!CommentEnd.IsMatch(scan.Text))
            {
                scan.Text = "";
                return;
            }

            scan.Text = CommentEnd.Replace(scan.Text, "", 1);
            scan.InComment = false;
        }

        scan.Text = FullComment.Replace(scan.Text, " ");
        if (CommentStart.IsMatch(scan.Text))
        {
            scan.Text = CommentStart.Replace(scan.Text, "", 1);
            scan.InComment = true;
        }
    }

    // True when the line is a preprocessor line, or continues one: the script's "goto preproc".
    private static bool IsPreprocessorLine(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        if (scan.Preprocessor && BackslashEnd.IsMatch(scan.PreviousPreprocessorLine))
        {
            return true;
        }

        scan.Preprocessor = false;
        WarnAtGroup(run, scan, CppComment, "CPPCOMMENTS", 0, "// comment");
        WarnAtGroup(run, scan, IfDefinedSingle, "IFDEFSINGLE", 0, "use ifdef/ifndef for single macro checks");
        CheckInclude(run, scan);
        if (SpaceBeforeHash.IsMatch(scan.Text))
        {
            Warn(run, scan, "CPPSPACE", 0, "space before preprocessor hash");
        }

        scan.Preprocessor = PreprocessorDirective.IsMatch(scan.Text);
        return scan.Preprocessor;
    }

    private static void CheckInclude(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        Match include = Include.Match(scan.Text);
        if (!include.Success)
        {
            return;
        }

        string path = include.Groups[2].Value;
        if (scan.Includes.ContainsKey(path))
        {
            Warn(run, scan, "INCLUDEDUP", include.Groups[1].Length, "duplicated include");
        }

        scan.Includes[path] = scan.Text;
    }

    private static void CheckSpaceBeforeParen(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        Match match = SpaceBeforeParen.Match(noStrings);
        if (!match.Success)
        {
            return;
        }

        string leading = match.Groups[1].Value;
        string word = match.Groups[2].Value;
        string name = match.Groups[3].Value;
        if (!IsAllowedLeading(leading) && !IsAllowedName(name) && !IsFunctionPointer(match.Groups[4].Value, word))
        {
            Warn(run, scan, "SPACEBEFOREPAREN", leading.Length + word.Length, $"{word} with space");
        }
    }

    // A #if line or a typedef may have a space before the paren.
    private static bool IsAllowedLeading(string leading) =>
        leading.Contains('#') || leading.Contains("typedef", StringComparison.Ordinal);

    // return and case must have the space.
    private static bool IsAllowedName(string name) => name is "return" or "case";

    // A "(*" after a name, not a control word, declares a function pointer.
    private static bool IsFunctionPointer(string first, string word) => first == "*" && !ControlWord.IsMatch(word);

    // Perl's truth of the text after the condition, and not blank or a continued condition.
    private static bool IsCodeAfterCondition(string afterParen) =>
        afterParen is not ("" or "0") && !BlankRest.IsMatch(afterParen) && !ContinuedCondition.IsMatch(afterParen);

    private static void CheckComparisons(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        WarnAtComparison(run, scan, EqualsNull.Match(noStrings), "EQUALSNULL", "we prefer !variable instead of \"== NULL\" comparisons");
        WarnAtComparison(run, scan, NotEqualsZero.Match(noStrings), "NOTEQUALSZERO", "we prefer if(rc) instead of \"rc != 0\" comparisons");
        Match doBrace = DoBrace.Match(noStrings);
        Match braceWhile;
        if (doBrace.Success && doBrace.Groups[2].Length != 1)
        {
            Warn(run, scan, "DOBRACE", doBrace.Groups[1].Length + 2, "one space after do before brace");
        }
        else if ((braceWhile = BraceWhile.Match(noStrings)).Success && braceWhile.Groups[2].Length != 1)
        {
            Warn(run, scan, "BRACEWHILE", braceWhile.Groups[1].Length + 2, "one space between brace and while");
        }
    }

    private static void WarnAtComparison(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, Match match, string name, string message)
    {
        if (match.Success)
        {
            Warn(run, scan, name, match.Groups[1].Length + match.Groups[2].Length + match.Groups[3].Length, message);
        }
    }

    private static void CheckCondition(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        Match match = IfCondition.Match(noStrings);
        if (!match.Success)
        {
            return;
        }

        string condition = match.Groups[4].Value;
        string afterParen = match.Groups[5].Value;
        if (condition.Contains(" = ", StringComparison.Ordinal))
        {
            Warn(run, scan, "ASSIGNWITHINCONDITION", match.Groups[1].Length + 1, "assignment within conditional expression");
        }

        bool even = condition.AsSpan().Count('(') == condition.AsSpan().Count(')');
        if (!scan.Text.Contains('#') && even && IsCodeAfterCondition(afterParen))
        {
            Warn(run, scan, "ONELINECONDITION", scan.Text.Length - afterParen.Length, "conditional block on the same line");
        }
    }

    private static void CheckOperators(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        WarnAtGroup(run, scan, SpaceAfterParen, "SPACEAFTERPAREN", 1, "space after open parenthesis");
        WarnAtGroup(run, scan, AndNoSpaceBefore, noStrings, "NOSPACEAND", 1, "missing space before Logical AND");
        WarnAtGroup(run, scan, AndNoSpaceAfter, noStrings, "NOSPACEAND", 0, "missing space after Logical AND");
    }

    // Character literals around a colon do not count: the script checks again with them taken out.
    private static void CheckColon(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings, Regex pattern, string message)
    {
        Match match = pattern.Match(noStrings);
        string withoutLiterals = QuotedColonPair.Replace(noStrings, "$1:$2").Replace("':'", "", StringComparison.Ordinal);
        if (match.Success && pattern.IsMatch(withoutLiterals))
        {
            Warn(run, scan, "NOSPACEC", match.Groups[1].Length + 1, message);
        }
    }

    // The script's s/'?'//g takes out every single quote: '?' is an optional quote followed by a quote.
    private static void CheckQuestionAndThan(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        Match before = QuestionNoSpaceBefore.Match(noStrings);
        if (before.Success && QuestionNoSpaceBefore.IsMatch(QuotedQuestion.Replace(noStrings, "")))
        {
            Warn(run, scan, "NOSPACEQ", before.Groups[1].Length + 1, "missing space before question mark");
        }

        WarnAtGroup(run, scan, QuestionNoSpaceAfter, noStrings, "NOSPACEQ", 1, "missing space after question mark");
        WarnAtGroup(run, scan, ThanNoSpaceBefore, noStrings, "NOSPACETHAN", 1, "missing space before less or greater than");
        WarnAtGroup(run, scan, ThanNoSpaceAfter, noStrings, "NOSPACETHAN", 1, "missing space after less or greater than");
    }

    private static void CheckParensAndReturn(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        WarnAtGroup(run, scan, SpaceBeforeClose, "SPACEBEFORECLOSE", 1, "space before close parenthesis");
        WarnAtGroup(run, scan, SpaceBeforeComma, "SPACEBEFORECOMMA", 1, "space before comma");
        Match returnNoSpace = ReturnNoSpace.Match(scan.Text);
        if (returnNoSpace.Success && !returnNoSpace.Groups[1].Value.Contains('#'))
        {
            Warn(run, scan, "RETURNNOSPACE", returnNoSpace.Groups[1].Length + 6, "return without space before paren");
        }

        WarnAtGroup(run, scan, ReturnParen, "RETURNPAREN", 7, "return with paren");
        Match sizeof_ = SizeofNoParen.Match(scan.Text);
        if (sizeof_.Success && sizeof_.Groups[2].Value != "(" && !sizeof_.Groups[1].Value.Contains('#'))
        {
            Warn(run, scan, "SIZEOFNOPAREN", sizeof_.Groups[1].Length + 6, "sizeof without parenthesis");
        }
    }
}
