using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// The per-line checks of <c>checksrc.pl</c>'s <c>scanfile</c> from script line 900 to 1268 (curl-8_22_0, BL-3418), in
/// the script's order: the code checks from the comma to the multiple spaces, skipped on a preprocessor line as the
/// script's <c>goto preproc</c> skips them, then the <c>preproc:</c> label's banned preprocessor symbols. Perl's
/// <c>\w</c>, <c>\W</c>, <c>\s</c>, <c>\S</c> and <c>\b</c> on a byte string match ASCII only, so the patterns spell
/// them out.
/// </summary>
internal static class UpstreamChecksrcScanLastChecks
{
    private const string Word = "[A-Za-z0-9_]";
    private const string NotWord = "[^A-Za-z0-9_]";
    private const string Space = @"[ \t\n\r\f\v]";

    private static readonly Regex QuotedString = Pattern("\".*\"");
    private static readonly Regex CommaNoSpace = Pattern(@"^(.*),[^ \n]");
    private static readonly Regex CloseBrace = Pattern(@"^( *)\}");
    private static readonly Regex FirstCharacter = Pattern("^( *)(.)");
    private static readonly Regex BraceElse = Pattern(@"^(.*)\} *else");
    private static readonly Regex ParenBrace = Pattern(@"^(.*)\)\{");
    private static readonly Regex OpenBraceFirst = Pattern(@"^\{");
    private static readonly Regex BlankLine = Pattern(@"^[ \t]*\z");
    private static readonly Regex SpaceSemicolon = Pattern("^(.*[^ ].*) ;$");
    private static readonly Regex SpaceSwitchColon = Pattern("^( *(case .+|default)) :");
    private static readonly Regex Label = Pattern("^ +([A-Za-z_][A-Za-z0-9_]*):$");
    private static readonly Regex FreeCall = Pattern(@"^( *)curlx_free\(([^)]+)\);");
    private static readonly Regex CallAfterNonWord = Pattern($@"^(.*?{NotWord})({Word}+)({Space}*\()");
    private static readonly Regex CallAfterParen = Pattern($@"^(.*?\()({Word}+)({Space}*\()");
    private static readonly Regex MemberAccess = Pattern(@"(->|\.)$");
    private static readonly Regex Stderr = Pattern($@"^([^""-]*{NotWord})(stderr)[^""_]");
    private static readonly Regex PreprocessorStart = Pattern(@"^ *#");
    private static readonly Regex FopenCall = Pattern($@"^(.*{NotWord})(curlx_fopen|CURLX_FOPEN_LOW|curlx_freopen|CURLX_FREOPEN_LOW){Space}*\([^,]*, *""([^""]*)");
    private static readonly Regex CloseParenEnd = Pattern(@"\)\z");
    private static readonly Regex IndentedOpenBrace = Pattern(@"^( +)\{");
    private static readonly Regex BlockStart = Pattern(@"^( *)((if|while|for)\(.*\{|else)\z");
    private static readonly Regex ConditionLine = Pattern(@"^( *)(if|while|for)(\(.*\))\z");
    private static readonly Regex Indent = Pattern("^( *)[^ ]");
    private static readonly Regex AsteriskSpace = Pattern($@"(^.*(char|int|long|void|CURL|CURLM|CURLMsg|[cC]url_[A-Za-z_]+|struct [a-zA-Z_]+) *(\*+)) ({Word}+)");
    private static readonly Regex AsteriskNoSpace = Pattern(@"(^.*(char|int|long|void|curl_slist|CURL|CURLM|CURLMsg|curl_httppost|sockaddr_in|FILE)\*)");
    private static readonly Regex FunctionBrace = Pattern($@"^(({Word}).*)\{{\z");
    private static readonly Regex Const = Pattern("const *");
    private static readonly Regex Static = Pattern("static *");
    private static readonly Regex Parens = Pattern(@"\(.*\)");
    private static readonly Regex EqualsNoSpace = Pattern("(.*)=[a-z0-9]", RegexOptions.IgnoreCase);
    private static readonly Regex NoSpaceEquals = Pattern("(.*)[a-z0-9]=", RegexOptions.IgnoreCase);
    private static readonly Regex EqualsSpace = Pattern("(.*)[a-z0-9] =  ", RegexOptions.IgnoreCase);
    private static readonly Regex PlusNoSpace = Pattern(@"(.*)[^+]\+[a-z0-9]", RegexOptions.IgnoreCase);
    private static readonly Regex NoSpacePlus = Pattern(@"(.*)[a-z0-9]\+[^+]", RegexOptions.IgnoreCase);
    private static readonly Regex SemicolonNoSpace = Pattern("(.*);[a-z0-9]", RegexOptions.IgnoreCase);
    private static readonly Regex TypedefStruct = Pattern(@"^(.*)typedef struct.*\{");
    private static readonly Regex ExclamationSpace = Pattern($@"(.*)! +({Word}|\()");
    private static readonly Regex VoidExclamation = Pattern(@"(.*)\(void\)!");
    private static readonly Regex ErrnoDefine = Pattern($"(.*)(?<!{Word})(EACCES|EADDRINUSE|EADDRNOTAVAIL|EAFNOSUPPORT|EBADF|ECONNREFUSED|ECONNRESET|EINPROGRESS|EINTR|EINVAL|EISCONN|EMSGSIZE|ENOMEM|ETIMEDOUT|EWOULDBLOCK)(?!{Word})");
    private static readonly Regex MultiSpace = Pattern(@"^(.*([^ \t\n\r\f\v])) + [{?]", RegexOptions.IgnoreCase);
    private static readonly Regex BannedPreprocessorSymbol = Pattern($"^(^|.*{NotWord})(WIN32)({NotWord}|$)");

    /// <summary>Runs the checks on the scan's current line, warning through <paramref name="run"/>.</summary>
    /// <param name="run">The run that counts and prints the warnings.</param>
    /// <param name="scan">The file being scanned, its current line in <see cref="UpstreamChecksrcRun.FileScan.Text"/>.</param>
    public static void Check(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(scan);
        if (!scan.Preprocessor)
        {
            CheckCode(run, scan);
        }

        // The script's "preproc:" label, reached by every line; only a preprocessor line is checked there.
        if (scan.Preprocessor)
        {
            WarnAtGroup(run, scan, BannedPreprocessorSymbol, scan.Text, "BANNEDPREPROC", 0, "use of WIN32 is banned from preprocessor lines, use _WIN32 instead");
        }
    }

    private static void CheckCode(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        string noStrings = QuotedString.Replace(scan.Text, "");
        CheckComma(run, scan);
        CheckBraces(run, scan);
        CheckSemicolonColonAndLabel(run, scan);
        CheckSafeFree(run, scan);
        CheckBannedFunctions(run, scan);
        CheckStderr(run, scan);
        CheckFopenMode(run, scan);
        CheckBracePosition(run, scan);
        CheckIndentation(run, scan);
        CheckAsterisks(run, scan);
        CheckFunctionBrace(run, scan);
        CheckEquals(run, scan, noStrings);
        CheckPlusAndSemicolon(run, scan, noStrings);
        CheckRest(run, scan, noStrings);
    }

    private static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options | RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static void Warn(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string name, int column, string text, string message) =>
        run.Warn(name, scan.Line, column, scan.File, text, message);

    // A warning at the first group's length plus an offset, naming the line as read, when the pattern matches text.
    private static void WarnAtGroup(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, Regex pattern, string text, string name, int offset, string message)
    {
        Match match = pattern.Match(text);
        if (match.Success)
        {
            Warn(run, scan, name, match.Groups[1].Length + offset, scan.Original, message);
        }
    }

    // A comma after a '#', a comment start or a double quote, or straight after a single quote, is not code.
    private static void CheckComma(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        Match match = CommaNoSpace.Match(scan.Text);
        if (!match.Success)
        {
            return;
        }

        string prefix = match.Groups[1].Value;
        bool ignored = prefix.Contains('#') || prefix.Contains("/*", StringComparison.Ordinal) || prefix.Contains('"') || prefix.EndsWith('\'');
        if (!ignored)
        {
            Warn(run, scan, "COMMANOSPACE", prefix.Length + 1, scan.Text, "comma without following space");
        }
    }

    private static void CheckBraces(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        CheckCloseBrace(run, scan);
        Match match = BraceElse.Match(scan.Text);
        if (match.Success)
        {
            Warn(run, scan, "BRACEELSE", match.Groups[1].Length, scan.Text, "else after closing brace on same line");
        }

        if ((match = ParenBrace.Match(scan.Text)).Success)
        {
            Warn(run, scan, "PARENBRACE", match.Groups[1].Length + 1, scan.Text, "missing space after close paren");
        }

        if (OpenBraceFirst.IsMatch(scan.Text) && BlankLine.IsMatch(scan.PreviousLine))
        {
            Warn(run, scan, "EMPTYLINEBRACE", 0, scan.Text, "empty line before open brace");
        }
    }

    // The previous line starting with a close brace is a legitimate oddity; the warning names the previous line.
    private static void CheckCloseBrace(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        Match close = CloseBrace.Match(scan.Text);
        Match previous = FirstCharacter.Match(scan.PreviousLine);
        if (!close.Success || !previous.Success)
        {
            return;
        }

        int previousIndent = previous.Groups[1].Length;
        if (close.Groups[1].Length + run.Indent > previousIndent && previous.Groups[2].Value != "}")
        {
            Warn(run, scan, "CLOSEBRACE", previousIndent, scan.PreviousLine, "Suspicious close brace indentation");
        }
    }

    private static void CheckSemicolonColonAndLabel(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        WarnAtGroup(run, scan, SpaceSemicolon, scan.Text, "SPACESEMICOLON", 0, "no space before semicolon");
        WarnAtGroup(run, scan, SpaceSwitchColon, scan.Text, "SPACESWITCHCOLON", 0, "no space before colon of switch label");
        Match label = Label.Match(scan.Text);
        if (!scan.PreviousLine.EndsWith('?') && label.Success && label.Groups[1].Value != "default")
        {
            Warn(run, scan, "SPACEBEFORELABEL", label.Groups[1].Length, scan.Original, "no space before label");
        }
    }

    // Perl's if($l) is false for an empty line and for "0", which keep the last curlx_free() call.
    private static void CheckSafeFree(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        string line = scan.Text;
        CheckNullAfterFree(run, scan);
        if (line is "" or "0")
        {
            return;
        }

        Match free = FreeCall.Match(line);
        scan.PreviousFreeIndent = free.Groups[1].Value;
        scan.PreviousFreeVariable = free.Groups[2].Value;
    }

    private static void CheckNullAfterFree(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        if (scan.PreviousFreeVariable != "" && scan.Text.StartsWith($"{scan.PreviousFreeIndent}{scan.PreviousFreeVariable} = NULL;", StringComparison.Ordinal))
        {
            Warn(run, scan, "USESAFEFREE", scan.PreviousFreeIndent.Length, scan.Original, "replace curlx_free() + NULL assignment with curlx_safefree()");
        }
    }

    // The script's "again:" loop: each banned call found is overwritten with x's, its suffix with one x, and the line
    // searched again; a call after "->" or "." is a member, not the function. The line is restored afterwards.
    private static void CheckBannedFunctions(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        string line = scan.Text;
        while (FindBannedCall(run, line) is Match call)
        {
            string prefix = call.Groups[1].Value;
            string bad = call.Groups[2].Value;
            if (!MemberAccess.IsMatch(prefix))
            {
                Warn(run, scan, "BANNEDFUNC", prefix.Length, scan.Original, $"use of {bad} is banned");
            }

            string search = prefix + bad + call.Groups[3].Value;
            int at = line.IndexOf(search, StringComparison.Ordinal);
            line = string.Concat(line.AsSpan(0, at), prefix + new string('x', bad.Length + 1), line.AsSpan(at + search.Length));
        }
    }

    // The first call after a non-word character, else the first call after an open paren, when its function is banned.
    private static Match? FindBannedCall(UpstreamChecksrcRun run, string line)
    {
        Match call = CallAfterNonWord.Match(line);
        if (call.Success && run.IsBanned(call.Groups[2].Value))
        {
            return call;
        }

        call = CallAfterParen.Match(line);
        return call.Success && run.IsBanned(call.Groups[2].Value) ? call : null;
    }

    // STDERR is an extended warning, checked only when .checksrc enables it, and never on a preprocessor line.
    private static void CheckStderr(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        if (!run.IsEnabled("STDERR"))
        {
            return;
        }

        Match match = Stderr.Match(scan.Text);
        if (match.Success && !PreprocessorStart.IsMatch(match.Groups[1].Value))
        {
            Warn(run, scan, "STDERR", match.Groups[1].Length, scan.Original, "use of stderr is banned (use tool_stderr instead)");
        }
    }

    private static void CheckFopenMode(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        Match match = FopenCall.Match(scan.Text);
        string mode = match.Groups[3].Value;
        if (match.Success && !mode.Contains('b'))
        {
            Warn(run, scan, "FOPENMODE", match.Groups[1].Length, scan.Original, $"use of non-binary fopen without FOPEN_* macro: {mode}");
        }
    }

    private static void CheckBracePosition(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        if (CloseParenEnd.IsMatch(scan.PreviousLine) && !scan.PreviousPreprocessor)
        {
            WarnAtGroup(run, scan, IndentedOpenBrace, scan.Text, "BRACEPOS", 0, "badly placed open brace");
        }
    }

    // After an if, while or for ending in an open brace, an else, or a whole condition, the line is indented one step.
    private static void CheckIndentation(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        if (scan.PreviousPreprocessor)
        {
            return;
        }

        Match block = BlockStart.Match(scan.PreviousLine);
        Match condition;
        if (block.Success)
        {
            CheckIndentedFrom(run, scan, block.Groups[1].Length);
        }
        else if ((condition = ConditionLine.Match(scan.PreviousLine)).Success && HasBalancedParens(condition.Groups[3].Value))
        {
            CheckIndentedFrom(run, scan, condition.Groups[1].Length);
        }
    }

    private static bool HasBalancedParens(string text) => text.AsSpan().Count('(') == text.AsSpan().Count(')');

    private static void CheckIndentedFrom(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, int first)
    {
        Match indent = Indent.Match(scan.Text);
        int second = indent.Groups[1].Length;
        if (indent.Success && first + run.Indent != second)
        {
            Warn(run, scan, "INDENTATION", second, scan.Original, $"not indented {run.Indent} steps (uses {second - first})");
        }
    }

    private static void CheckAsterisks(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        Match match = AsteriskSpace.Match(scan.Text);
        if (match.Success && match.Groups[4].Value is not ("const" or "volatile"))
        {
            Warn(run, scan, "ASTERISKSPACE", match.Groups[1].Length, scan.Original, "space after declarative asterisk");
        }

        WarnAtGroup(run, scan, AsteriskNoSpace, scan.Text, "ASTERISKNOSPACE", -1, "no space before asterisk");
    }

    // The first "const" and "static" are taken out; an open and a close paren before the brace make it a function.
    private static void CheckFunctionBrace(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan)
    {
        Match match = FunctionBrace.Match(scan.Text);
        if (match.Success && Parens.IsMatch(Static.Replace(Const.Replace(match.Groups[1].Value, "", 1), "", 1)))
        {
            Warn(run, scan, "BRACEPOS", scan.Text.Length - 1, scan.Original, "wrongly placed open brace");
        }
    }

    private static void CheckEquals(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        if (EqualsNoSpace.IsMatch(noStrings))
        {
            WarnAtGroup(run, scan, EqualsNoSpace, noStrings, "EQUALSNOSPACE", 1, "no space after equals sign");
        }
        else if (NoSpaceEquals.IsMatch(noStrings))
        {
            WarnAtGroup(run, scan, NoSpaceEquals, noStrings, "NOSPACEEQUALS", 1, "no space before equals sign");
        }
        else
        {
            WarnAtGroup(run, scan, EqualsSpace, scan.Text, "EQUALSPACE", 3, "more than one space after equals sign");
        }
    }

    private static void CheckPlusAndSemicolon(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        if (PlusNoSpace.IsMatch(noStrings))
        {
            WarnAtGroup(run, scan, PlusNoSpace, noStrings, "PLUSNOSPACE", 1, "no space after plus sign");
        }
        else
        {
            WarnAtGroup(run, scan, NoSpacePlus, noStrings, "NOSPACEPLUS", 1, "no space before plus sign");
        }

        WarnAtGroup(run, scan, SemicolonNoSpace, noStrings, "SEMINOSPACE", 1, "no space after semicolon");
    }

    // The multiple-spaces check skips a line with strings in it, whose spaces may be the string's.
    private static void CheckRest(UpstreamChecksrcRun run, UpstreamChecksrcRun.FileScan scan, string noStrings)
    {
        WarnAtGroup(run, scan, TypedefStruct, noStrings, "TYPEDEFSTRUCT", 1, "typedef'ed struct");
        WarnAtGroup(run, scan, ExclamationSpace, noStrings, "EXCLAMATIONSPACE", 1, "space after exclamation mark");
        WarnAtGroup(run, scan, VoidExclamation, noStrings, "VOIDEXCL", 1, "exclamation after (void) is weird");
        Match errno = ErrnoDefine.Match(noStrings);
        if (errno.Success)
        {
            Warn(run, scan, "ERRNOVAR", errno.Groups[1].Length, scan.Original, $"use of bare errno define {errno.Groups[2].Value}, use SOCK{errno.Groups[2].Value}");
        }

        if (scan.Text == noStrings)
        {
            WarnAtGroup(run, scan, MultiSpace, noStrings, "MULTISPACE", 1, "multiple spaces");
        }
    }
}
