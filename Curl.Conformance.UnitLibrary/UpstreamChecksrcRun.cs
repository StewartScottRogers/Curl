using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// One run of the <see cref="UpstreamChecksrcScript"/> interpretation of <c>checksrc.pl</c>: its option and
/// <c>.checksrc</c> state, its counters, <c>checkwarn</c>, the <c>!checksrc!</c> instructions and the frame of its
/// <c>scanfile</c> (BL-3416).
/// </summary>
/// <remarks>
/// The per-line checks plug into <see cref="CheckLineFirstHalf"/> (script lines 532-900, BL-3417) and
/// <see cref="CheckLineSecondHalf"/> (script lines 900-1268, BL-3418), called in the script's order with the file's
/// <see cref="FileScan"/>.
/// </remarks>
internal sealed class UpstreamChecksrcRun
{
    private const int AcceptAll = 999999;

    private static readonly string[] BannedFunctions =
    [
        "_access", "_fstati64", "_lseeki64", "_mbscat", "_mbsncat", "_open", "_tcscat", "_tcsdup", "_tcsncat", "_tcsncpy",
        "_waccess", "_wcscat", "_wcsdup", "_wcsncat", "_wfopen", "_wfreopen", "_wopen", "abort", "accept", "accept4", "access",
        "aprintf", "assert", "atoi", "atol", "calloc", "close", "CreateFile", "CreateFileA", "CreateFileW", "fclose", "fdopen",
        "fopen", "fprintf", "free", "freeaddrinfo", "freopen", "fstat", "getaddrinfo", "gets", "gmtime", "inet_ntop",
        "inet_pton", "llseek", "LoadLibrary", "LoadLibraryA", "LoadLibraryEx", "LoadLibraryExA", "LoadLibraryExW",
        "LoadLibraryW", "localtime", "lseek", "malloc", "mbstowcs", "MoveFileEx", "MoveFileExA", "MoveFileExW", "msnprintf",
        "mvsnprintf", "open", "printf", "realloc", "recv", "rename", "send", "snprintf", "socket", "socketpair", "sprintf",
        "sscanf", "stat", "strcat", "strcpy", "strdup", "strerror", "strncat", "strncpy", "strtok_r", "strtok", "strtol",
        "strtoul", "vaprintf", "vfprintf", "vprintf", "vsnprintf", "vsprintf", "wcscpy", "wcsdup", "wcsncpy", "wcstombs",
        "WSASocket", "WSASocketA", "WSASocketW",
    ];

    private static readonly Dictionary<string, string> ExtendedWarnings = new(StringComparer.Ordinal)
    {
        ["COPYRIGHTYEAR"] = "copyright year incorrect",
        ["STDERR"] = "stderr detected",
    };

    private static readonly Regex DirectoryOption = new("^-D(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex SkipOption = new("^-W(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex BanOption = new("^-b(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex AllowOption = new("^-a(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex AcceptOption = new("^-A(.+)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex IndentOption = new("^-i([1-9])", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex ColumnOption = new("^-m([0-9]+)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex CommentOrEmptyRcLine = new(@"^(\s*#.*|)$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex EnableRcLine = new("^enable ([A-Z]+)$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex DisableRcLine = new("^disable ([A-Z]+)$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex BanRcLine = new("^banfunc ([^ ]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex AllowRcLine = new("^allowfunc ([^ ]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex InstructionLine = new(@"\!checksrc\! (.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex Instruction = new(" *([^ ]*) *(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex InstructionCommentEnd = new(@" *\*/$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex DisableScope = new("([^ ]*) +(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex LeadingNumber = new(@"^\d+", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);
    private static readonly Regex LineDirective = new("^#line (\\d+) \"([^\"]*)\"", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private readonly IReadOnlyList<string> arguments;
    private readonly StringBuilder output = new();
    private readonly StringBuilder error = new();
    private readonly Dictionary<string, string?> bannedFunctions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> warnings = StandardWarnings();
    private readonly List<string> acceptList = [];
    private readonly HashSet<string> skipList = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> ignore = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> ignoreSet = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> ignoreUsed = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> ignoreLine = [];
    private int nextArgument;
    private string directory = ".";
    private string skippedFiles = "";
    private bool verbose;
    private int warningCount;
    private int errorCount;
    private int suppressedWarnings;
    private int suppressedErrors;

    /// <summary>Starts a run over the script's arguments.</summary>
    /// <param name="arguments">The options and files.</param>
    public UpstreamChecksrcRun(IReadOnlyList<string> arguments)
    {
        this.arguments = arguments;
        foreach (string function in BannedFunctions)
        {
            bannedFunctions[function] = "1";
        }
    }

    private enum Argument
    {
        Option,
        Help,
        File,
    }

    /// <summary>The indent step <c>-i</c> set: 2 unless given.</summary>
    public int Indent { get; private set; } = 2;

    /// <summary>The longest line <c>-m</c> allows: 79 unless given.</summary>
    public int MaxColumn { get; private set; } = 79;

    /// <summary>Every function the script knows by name, banned or allowed again, as <c>keys %banfunc</c>.</summary>
    public IEnumerable<string> KnownFunctions => bannedFunctions.Keys;

    /// <summary>Whether <c>$banfunc{function}</c> is true: the function is banned.</summary>
    /// <param name="function">The function's name.</param>
    /// <returns>True when a call to it is reported.</returns>
    public bool IsBanned(string function) => bannedFunctions.TryGetValue(function, out string? value) && IsTrue(value);

    /// <summary>Whether <c>$warnings{name}</c> is set: a standard warning, or an extended one <c>.checksrc</c> enabled.</summary>
    /// <param name="name">The warning's name.</param>
    /// <returns>True when the warning is checked.</returns>
    public bool IsEnabled(string name) => warnings.ContainsKey(name);

    /// <summary>Takes the options, checks every file and writes the summary, as the script's main program does.</summary>
    /// <returns>What the script writes and its exit code.</returns>
    public UpstreamChecksrcScript.Result Run()
    {
        string? file = TakeOptions();
        if (!IsTrue(file))
        {
            WriteUsage();
            return Finish(0);
        }

        ReadSkipList();
        ReadLocalFile(file!);
        return ScanFiles(file);
    }

    // The script's option loop: the first file, or null after -h or with no file.
    private string? TakeOptions()
    {
        string? word = NextArgument();
        Argument kind;
        while (word is not null && (kind = TakeOption(word)) != Argument.File)
        {
            if (kind == Argument.Help)
            {
                return null;
            }

            word = NextArgument();
        }

        return word;
    }

    // The script's file loop: false from scanfile ends it at once, with exit code 0.
    private UpstreamChecksrcScript.Result ScanFiles(string? file)
    {
        do
        {
            if (!Regex.IsMatch(skippedFiles, $" {file} ", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout) && !ScanFile(FullName(file!)))
            {
                return Finish(0);
            }

            file = NextArgument();
        }
        while (IsTrue(file));

        return Summary();
    }

    /// <summary>
    /// <c>checkwarn</c>: counts and prints one problem, unless a <c>checksrc.skip</c> line or a <c>!checksrc!</c>
    /// instruction suppresses it.
    /// </summary>
    /// <param name="name">The warning's name.</param>
    /// <param name="number">The line number.</param>
    /// <param name="column">The zero-based column the caret points at.</param>
    /// <param name="file">The file name the message names.</param>
    /// <param name="text">The line as read.</param>
    /// <param name="message">The message.</param>
    /// <param name="isError">True for an error, false for a warning.</param>
    public void Warn(string name, int number, int column, string file, string text, string message, bool isError = false)
    {
        if (IsSuppressed(name, number, file, text))
        {
            _ = isError ? suppressedErrors++ : suppressedWarnings++;
            return;
        }

        _ = isError ? errorCount++ : warningCount++;
        column++;
        output.Append(CultureInfo.InvariantCulture, $"{file}:{number}:{column}: {(isError ? "error" : "warning")}: {message} ({name})\n {text}\n");
        if (column < 80)
        {
            output.Append(' ', column).Append("^\n");
        }
    }

    /// <summary>
    /// The script's checks of script lines 532-900 for one line (<see cref="UpstreamChecksrcScanFirstChecks"/>); a
    /// preprocessor line leaves <see cref="FileScan.Preprocessor"/> set for the script's <c>goto preproc</c>.
    /// </summary>
    /// <param name="scan">The file being scanned, its current line in <see cref="FileScan.Text"/>.</param>
    internal void CheckLineFirstHalf(FileScan scan) => UpstreamChecksrcScanFirstChecks.Check(this, scan);

    /// <summary>
    /// The script's checks of script lines 900-1268 for one line (<see cref="UpstreamChecksrcScanLastChecks"/>), after
    /// <see cref="CheckLineFirstHalf"/>.
    /// </summary>
    /// <param name="scan">The file being scanned, its current line in <see cref="FileScan.Text"/>.</param>
    internal void CheckLineSecondHalf(FileScan scan) => UpstreamChecksrcScanLastChecks.Check(this, scan);

    /// <summary>
    /// The script's checks after the last line: <c>COPYRIGHT</c>, <c>OPENCOMMENT</c> and <c>checksrc_endoffile</c>'s
    /// unused ignores. <c>COPYRIGHTYEAR</c> asks git about the file and never warns here: the file is in no repository.
    /// </summary>
    /// <param name="scan">The file scanned.</param>
    internal void CheckEndOfFile(FileScan scan)
    {
        if (scan.Copyright.Count == 0)
        {
            Warn("COPYRIGHT", 1, 0, scan.File, "", "Missing copyright statement", true);
        }

        if (scan.InComment)
        {
            Warn("OPENCOMMENT", 1, 0, scan.File, "", "Missing closing comment", true);
        }

        foreach ((string name, int number) in ignoreSet.ToList())
        {
            if (number != 0 && ignoreUsed.GetValueOrDefault(name) == 0)
            {
                Warn("UNUSEDIGNORE", number, name.Length + 11, scan.File, ignoreLine.GetValueOrDefault(number, ""), $"Unused ignore: {name}");
            }
        }
    }

    // Perl's truth: undef, "" and "0" are false.
    private static bool IsTrue(string? value) => value is not (null or "" or "0");

    private string? NextArgument() => nextArgument < arguments.Count ? arguments[nextArgument++] : null;

    private UpstreamChecksrcScript.Result Finish(int exitCode) => new(output.ToString(), error.ToString(), exitCode);

    private UpstreamChecksrcScript.Result Summary()
    {
        bool reported = errorCount > 0 || warningCount > 0;
        if (reported || verbose)
        {
            output.Append(CultureInfo.InvariantCulture, $"checksrc: {errorCount} errors and {warningCount} warnings\n");
            if (suppressedErrors + suppressedWarnings > 0)
            {
                output.Append(CultureInfo.InvariantCulture, $"checksrc: {suppressedErrors} errors and {suppressedWarnings} warnings suppressed\n");
            }
        }

        return Finish(reported ? 5 : 0);
    }

    private Argument TakeOption(string word)
    {
        Match match;
        if ((match = DirectoryOption.Match(word)).Success)
        {
            directory = match.Groups[1].Value;
        }
        else if ((match = SkipOption.Match(word)).Success)
        {
            skippedFiles += $" {match.Groups[1].Value} ";
        }
        else if ((match = BanOption.Match(word)).Success)
        {
            bannedFunctions[match.Groups[1].Value] = match.Groups[1].Value;
        }
        else if ((match = AllowOption.Match(word)).Success)
        {
            bannedFunctions[match.Groups[1].Value] = null;
        }
        else if ((match = AcceptOption.Match(word)).Success)
        {
            acceptList.Add(match.Groups[1].Value);
        }
        else
        {
            return TakeNumberOption(word);
        }

        return Argument.Option;
    }

    private Argument TakeNumberOption(string word)
    {
        Match match;
        if ((match = IndentOption.Match(word)).Success)
        {
            Indent = match.Groups[1].Value[0] - '0';
        }
        else if ((match = ColumnOption.Match(word)).Success)
        {
            MaxColumn = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        else if (word.StartsWith("-v", StringComparison.Ordinal))
        {
            verbose = true;
        }
        else
        {
            return word.StartsWith("-h", StringComparison.Ordinal) || word.StartsWith("--help", StringComparison.Ordinal) ? Argument.Help : Argument.File;
        }

        return Argument.Option;
    }

    private void WriteUsage()
    {
        output.Append("checksrc.pl [option] <file1> [file2] ...\n");
        output.Append(" Options:\n");
        output.Append("  -A[rule]  Accept this violation, can be used multiple times\n");
        output.Append("  -a[func]  Allow use of this function\n");
        output.Append("  -b[func]  Ban use of this function\n");
        output.Append("  -D[DIR]   Directory to prepend filenames\n");
        output.Append("  -h        Show help output\n");
        output.Append("  -W[file]  Skip the given file - ignore all its flaws\n");
        output.Append("  -i<n>     Indent spaces. Default: 2\n");
        output.Append("  -m<n>     Maximum line length. Default: 79\n");
        output.Append("  -v        Verbose\n");
        output.Append("\nDetects and warns for these problems:\n");
        foreach (string name in warnings.Keys.Concat(ExtendedWarnings.Keys).Order(StringComparer.Ordinal))
        {
            output.Append(warnings.TryGetValue(name, out string? description)
                ? $" {name,-18}: {description}\n"
                : $" {name,-18}: {ExtendedWarnings[name]}[*]\n");
        }

        output.Append(" [*] = disabled by default\n");
        output.Append("\nDetects and bans use of these functions:\n");
        foreach (string function in bannedFunctions.Keys.Order(StringComparer.Ordinal))
        {
            output.Append(CultureInfo.InvariantCulture, $" {function,-18}\n");
        }
    }

    // The harness's paths are absolute: one written after "./" or the -D directory is opened as it is.
    private static string OpenPath(string path) =>
        path.StartsWith("./", StringComparison.Ordinal) && Path.IsPathRooted(path[2..]) ? path[2..] : path;

    // The -D directory goes in front of every path but one starting "./" or "../", an absolute one too (BL-3413).
    private string FullName(string file) =>
        file.StartsWith("./", StringComparison.Ordinal) || file.StartsWith("../", StringComparison.Ordinal) ? file : $"{directory}/{file}";

    // Perl's line reading with the script's Windows rule: each line loses a trailing CR LF or LF.
    private static List<string> ReadLines(string path)
    {
        List<string> lines = [.. File.ReadAllText(OpenPath(path), Encoding.Latin1).Split('\n')];
        string last = lines[^1];
        lines.RemoveAt(lines.Count - 1);
        lines = [.. lines.Select(line => line.EndsWith('\r') ? line[..^1] : line)];
        if (last.Length > 0)
        {
            lines.Add(last);
        }

        return lines;
    }

    private void ReadSkipList()
    {
        string path = $"{directory}/checksrc.skip";
        if (File.Exists(OpenPath(path)))
        {
            skipList.UnionWith(ReadLines(path));
        }
    }

    // With the directory "." and a file in a folder, the .checksrc in the file's folder; else the directory's.
    private string LocalFilePath(string file) =>
        directory == "." && file.Contains('/', StringComparison.Ordinal)
            ? $"{directory}/{file[..file.LastIndexOf('/')]}/.checksrc"
            : $"{directory}/.checksrc";

    private void ReadLocalFile(string file)
    {
        string path = LocalFilePath(file);
        if (!File.Exists(OpenPath(path)))
        {
            return;
        }

        int number = 0;
        foreach (string line in ReadLines(path))
        {
            number++;
            if (!CommentOrEmptyRcLine.IsMatch(line) && !TakeRcLine(line))
            {
                throw new InvalidDataException($"Invalid format in {directory}/.checksrc on line {number}: {line}");
            }
        }
    }

    // One instruction of .checksrc; false for a line the script dies on.
    private bool TakeRcLine(string line)
    {
        Match match;
        if ((match = EnableRcLine.Match(line)).Success)
        {
            EnableExtendedWarning(match.Groups[1].Value);
        }
        else if ((match = DisableRcLine.Match(line)).Success)
        {
            DisableWarning(match.Groups[1].Value);
        }
        else if ((match = BanRcLine.Match(line)).Success)
        {
            bannedFunctions[match.Groups[1].Value] = match.Groups[1].Value;
        }
        else if ((match = AllowRcLine.Match(line)).Success)
        {
            bannedFunctions[match.Groups[1].Value] = null;
        }
        else
        {
            return false;
        }

        return true;
    }

    private void EnableExtendedWarning(string name)
    {
        if (ExtendedWarnings.TryGetValue(name, out string? description))
        {
            warnings[name] = description;
        }
        else
        {
            error.Append(CultureInfo.InvariantCulture, $"invalid warning specified in .checksrc: \"{name}\"\n");
        }
    }

    private void DisableWarning(string name)
    {
        if (warnings.ContainsKey(name))
        {
            acceptList.Add(name);
        }
        else
        {
            error.Append(CultureInfo.InvariantCulture, $"invalid warning specified in .checksrc: \"{name}\"\n");
        }
    }

    // scanfile: false when accept_violations ends the script.
    private bool ScanFile(string file)
    {
        if (verbose)
        {
            output.Append(CultureInfo.InvariantCulture, $"Checking file: {file}\n");
        }

        List<string> lines = ReadLines(file);
        ignore.Clear();
        ignoreSet.Clear();
        ignoreLine.Clear();
        if (!AcceptViolations())
        {
            return false;
        }

        FileScan scan = new(file);
        foreach (string line in lines)
        {
            ScanLine(scan, line);
        }

        CheckEndOfFile(scan);
        return true;
    }

    private void ScanLine(FileScan scan, string line)
    {
        scan.StartLine(line);
        Match match = InstructionLine.Match(line);
        if (match.Success)
        {
            TakeInstruction(match.Groups[1].Value, scan);
        }

        // A #line instruction renames the file and renumbers the lines, and skips the line's checks.
        if ((match = LineDirective.Match(line)).Success)
        {
            scan.File = match.Groups[2].Value;
            scan.Line = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return;
        }

        CheckLineFirstHalf(scan);
        CheckLineSecondHalf(scan);
        scan.EndLine();
    }

    private bool IsSuppressed(string name, int number, string file, string text)
    {
        if (skipList.Contains(text))
        {
            return true;
        }

        if (ignore.GetValueOrDefault(name) == 0)
        {
            return false;
        }

        ignore[name]--;
        ignoreUsed[name] = ignoreUsed.GetValueOrDefault(name) + 1;
        if (ignore[name] == 0)
        {
            EnableWarning(name, number, file, text);
        }

        return true;
    }

    private void EnableWarning(string name, int number, string file, string text)
    {
        if (ignoreUsed.GetValueOrDefault(name) == 0)
        {
            Warn("UNUSEDIGNORE", number, name.Length + 11, file, text, "No warning was inhibited!");
        }

        ignoreSet[name] = 0;
        ignoreUsed[name] = 0;
        ignore[name] = 0;
    }

    private bool AcceptViolations()
    {
        foreach (string rule in acceptList)
        {
            if (!warnings.ContainsKey(rule))
            {
                output.Append(CultureInfo.InvariantCulture, $"'{rule}' is not a warning to accept!\n");
                return false;
            }

            ignore[rule] = AcceptAll;
            ignoreUsed[rule] = 0;
        }

        return true;
    }

    // A !checksrc! instruction: disable a warning for some lines, or enable it again.
    private void TakeInstruction(string instruction, FileScan scan)
    {
        Match match = Instruction.Match(instruction);
        string what = InstructionCommentEnd.Replace(match.Groups[2].Value, "", 1);
        switch (match.Groups[1].Value)
        {
            case "disable":
                Disable(what, scan);
                break;
            case "enable":
                EnableWarning(what, scan.Line, scan.File, scan.Original);
                break;
            default:
                Warn("BADCOMMAND", scan.Line, 0, scan.File, scan.Original, "Illegal !checksrc! command");
                break;
        }
    }

    private void Disable(string what, FileScan scan)
    {
        Match match = DisableScope.Match(what);
        (string name, string scope) = match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (what, "1");
        scope = scope == "all" ? AcceptAll.ToString(CultureInfo.InvariantCulture) : scope;
        if (scope == "0")
        {
            Warn("BADCOMMAND", scan.Line, 0, scan.File, scan.Original, "Disable zero not supported, did you mean to enable?");
        }
        else if (ignoreSet.GetValueOrDefault(name) != 0)
        {
            Warn("BADCOMMAND", scan.Line, 0, scan.File, scan.Original, $"{name} already disabled from line {ignoreSet[name]}");
        }
        else
        {
            ignore[name] = ScopeCount(scope);
            ignoreSet[name] = scan.Line;
            ignoreLine[scan.Line] = scan.Original;
        }
    }

    // Perl's number for the scope: its leading digits; a true scope without a number never counts down to zero.
    private static long ScopeCount(string scope)
    {
        Match number = LeadingNumber.Match(scope);
        long count = number.Success ? long.Parse(number.Value, CultureInfo.InvariantCulture) : 0;
        return count == 0 && IsTrue(scope) ? -1 : count;
    }

    private static Dictionary<string, string> StandardWarnings() => new(StringComparer.Ordinal)
    {
        ["ASSIGNWITHINCONDITION"] = "assignment within conditional expression",
        ["ASTERISKNOSPACE"] = "pointer declared without space before asterisk",
        ["ASTERISKSPACE"] = "pointer declared with space after asterisk",
        ["BADCOMMAND"] = "bad !checksrc! instruction",
        ["BANNEDFUNC"] = "a banned function was used",
        ["BANNEDPREPROC"] = "a banned symbol was used on a preprocessor line",
        ["BRACEELSE"] = "} else on the same line",
        ["BRACEPOS"] = "wrong position for an open brace",
        ["BRACEWHILE"] = "A single space between open brace and while",
        ["CLOSEBRACE"] = "close brace indent level vs line above is off",
        ["COMMANOSPACE"] = "comma without following space",
        ["COMMENTNOSPACEEND"] = "no space before */",
        ["COMMENTNOSPACESTART"] = "no space following /*",
        ["COPYRIGHT"] = "file missing a copyright statement",
        ["CPPCOMMENTS"] = "// comment detected",
        ["CPPSPACE"] = "space before preprocessor hash",
        ["DOBRACE"] = "A single space between do and open brace",
        ["EMPTYLINEBRACE"] = "Empty line before the open brace",
        ["EQUALSNOSPACE"] = "equals sign without following space",
        ["EQUALSNULL"] = "if/while comparison with == NULL",
        ["EQUALSPACE"] = "equals sign with too many spaces following",
        ["ERRNOVAR"] = "use of bare errno define",
        ["EXCLAMATIONSPACE"] = "Whitespace after exclamation mark in expression",
        ["FIXME"] = "FIXME or TODO comment",
        ["FOPENMODE"] = "fopen needs a macro for the mode string",
        ["IFDEFSINGLE"] = "use ifdef/ifndef for single macro checks",
        ["INCLUDEDUP"] = "same file is included again",
        ["INDENTATION"] = "wrong start column for code",
        ["LONGLINE"] = "Line longer than 79",
        ["MULTISPACE"] = "multiple spaces used when not suitable",
        ["NOSPACEAND"] = "missing space around Logical AND operator",
        ["NOSPACEC"] = "missing space around ternary colon operator",
        ["NOSPACEEQUALS"] = "equals sign without preceding space",
        ["NOSPACEPLUS"] = "no space before plus sign",
        ["NOSPACEQ"] = "missing space around ternary question mark operator",
        ["NOSPACETHAN"] = "missing space around less or greater than",
        ["NOTEQUALSZERO"] = "if/while comparison with != 0",
        ["ONELINECONDITION"] = "conditional block on the same line as the if()",
        ["OPENCOMMENT"] = "file ended with a /* comment still \"open\"",
        ["PARENBRACE"] = "){ without sufficient space",
        ["PLUSNOSPACE"] = "no space after plus sign",
        ["RETURNNOSPACE"] = "return without space",
        ["RETURNPAREN"] = "return with paren",
        ["SEMINOSPACE"] = "semicolon without following space",
        ["SIZEOFNOPAREN"] = "use of sizeof without parentheses",
        ["SPACEAFTERPAREN"] = "space after open parenthesis",
        ["SPACEBEFORECLOSE"] = "space before a close parenthesis",
        ["SPACEBEFORECOMMA"] = "space before a comma",
        ["SPACEBEFORELABEL"] = "labels not at the start of the line",
        ["SPACEBEFOREPAREN"] = "space before an open parenthesis",
        ["SPACESEMICOLON"] = "space before semicolon",
        ["SPACESWITCHCOLON"] = "space before colon of switch label",
        ["TABS"] = "TAB characters not allowed",
        ["TRAILINGSPACE"] = "Trailing whitespace on the line",
        ["TYPEDEFSTRUCT"] = "typedefed struct",
        ["UNUSEDIGNORE"] = "a warning ignore was not used",
        ["USESAFEFREE"] = "replace curlx_free() + NULL assignment with curlx_safefree()",
        ["VOIDEXCL"] = "(void)! is not something we like",
    };

    /// <summary>One file's <c>scanfile</c> state, shared by the per-line checks.</summary>
    /// <param name="file">The file name warnings name, until a <c>#line</c> instruction renames it.</param>
    internal sealed class FileScan(string file)
    {
        /// <summary>The file name warnings name (<c>$file</c>).</summary>
        public string File { get; set; } = file;

        /// <summary>The current line's number (<c>$line</c>).</summary>
        public int Line { get; set; } = 1;

        /// <summary>The current line as the checks change it (<c>$l</c>).</summary>
        public string Text { get; set; } = "";

        /// <summary>The current line as read, for messages (<c>$ol</c>).</summary>
        public string Original { get; private set; } = "";

        /// <summary>The column a check found (<c>$column</c>), 0 at the start of each line.</summary>
        public int Column { get; set; }

        /// <summary>The last line that was not a preprocessor line (<c>$prevl</c>).</summary>
        public string PreviousLine { get; private set; } = "";

        /// <summary>The last preprocessor line (<c>$prevpl</c>).</summary>
        public string PreviousPreprocessorLine { get; private set; } = "";

        /// <summary>Whether the current line is a preprocessor line (<c>$prep</c>).</summary>
        public bool Preprocessor { get; set; }

        /// <summary>Whether the line before was a preprocessor line (<c>$prevp</c>).</summary>
        public bool PreviousPreprocessor { get; private set; }

        /// <summary>The indent of the last <c>curlx_free()</c> call (<c>$prevfreeindent</c>).</summary>
        public string PreviousFreeIndent { get; set; } = "";

        /// <summary>The variable the last <c>curlx_free()</c> call freed (<c>$prevfreevar</c>).</summary>
        public string PreviousFreeVariable { get; set; } = "";

        /// <summary>Whether a <c>/*</c> comment is still open (<c>$incomment</c>).</summary>
        public bool InComment { get; set; }

        /// <summary>The copyright years found (<c>@copyright</c>).</summary>
        public List<CopyrightYear> Copyright { get; } = [];

        /// <summary>Each included path and the line that included it (<c>%includes</c>).</summary>
        public Dictionary<string, string> Includes { get; } = new(StringComparer.Ordinal);

        /// <summary>Takes the next line read: <c>$l</c> and <c>$ol</c> are the line, <c>$column</c> is 0.</summary>
        /// <param name="line">The line, its line ending removed.</param>
        public void StartLine(string line)
        {
            Text = line;
            Original = line;
            Column = 0;
        }

        /// <summary>The end of the script's loop body: counts the line and remembers it as the line before.</summary>
        public void EndLine()
        {
            Line++;
            PreviousPreprocessor = Preprocessor;
            if (Preprocessor)
            {
                PreviousPreprocessorLine = Original;
            }
            else
            {
                PreviousLine = Original;
            }
        }
    }

    /// <summary>One copyright year the scan found.</summary>
    /// <param name="Year">The year, or -1 for a statement without one.</param>
    /// <param name="Line">The line it is on.</param>
    /// <param name="Column">Its zero-based column.</param>
    /// <param name="Code">The line.</param>
    internal readonly record struct CopyrightYear(int Year, int Line, int Column, string Code);
}
