namespace Curl.Conformance;

/// <summary>
/// Emulates upstream's <c>tests/libtest/test610.pl</c> (curl 8.21.0), which cases run as
/// <c>%PERL %SRCDIR/libtest/test610.pl</c> in a precheck or postcheck to make, remove, move or
/// check for files, without running Perl, which the harness neither has nor needs.
/// </summary>
/// <remarks>
/// The script takes verbs and their paths, one after another: <c>mkdir DIR</c>, <c>rmdir DIR</c>,
/// <c>rm FILE</c>, <c>move FROM TO</c> and <c>gone PATH</c> (which fails when PATH exists). It
/// stops at the first verb that fails, with Perl's <c>die "$!"</c> exit code, the error number:
/// 2 (<c>ENOENT</c>) for a missing path, 17 (<c>EEXIST</c>) for a folder that already exists,
/// 39 (Linux's <c>ENOTEMPTY</c>) for a folder that is not empty, and 255 for <c>gone</c>, whose
/// <c>die</c> follows a successful test and so has no error number. Fewer than two arguments
/// print the usage and exit 1, and so does an unknown verb, with <c>Unsupported command VERB</c>.
/// </remarks>
internal static class UpstreamTest610Script
{
    private const int NoSuchFile = 2;

    private const int AlreadyExists = 17;

    private const int NotEmpty = 39;

    private const int Die = 255;

    private const string ScriptName = "test610.pl";

    private static readonly UpstreamPerlOneLinerResult Passed = new(0, "");

    /// <summary>Runs a <c>%PERL</c> line when its program is <c>test610.pl</c>.</summary>
    /// <param name="arguments">The expanded line after the Perl program's name, starting with the script's path.</param>
    /// <returns>What the script did, or <see langword="null"/> when the line runs another program.</returns>
    public static UpstreamPerlOneLinerResult? Run(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string[] words = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || Path.GetFileName(words[0]) != ScriptName)
        {
            return null;
        }

        return words.Length <= 2
            ? new(1, $"Usage: {words[0]} mkdir|rmdir|rm|move|gone path1 [path2] [more commands...]\n")
            : RunVerbs(new Queue<string>(words.Skip(1)));
    }

    private static UpstreamPerlOneLinerResult RunVerbs(Queue<string> words)
    {
        while (words.TryDequeue(out string? verb))
        {
            string path = words.TryDequeue(out string? next) ? next : "";
            int? exitCode = verb switch
            {
                "mkdir" => MakeFolder(path),
                "rmdir" => RemoveFolder(path),
                "rm" => File.Exists(path) ? Done(() => File.Delete(path)) : NoSuchFile,
                "move" => Move(path, words.TryDequeue(out string? target) ? target : ""),
                "gone" => Path.Exists(path) ? Die : 0,
                _ => null,
            };
            if (exitCode is null)
            {
                return new(1, $"Unsupported command {verb}\n");
            }

            if (exitCode != 0)
            {
                return new(exitCode.Value, "");
            }
        }

        return Passed;
    }

    private static int MakeFolder(string path) =>
        Path.Exists(path) ? AlreadyExists
        : Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(path))) ? Done(() => Directory.CreateDirectory(path))
        : NoSuchFile;

    private static int RemoveFolder(string path) =>
        !Directory.Exists(path) ? NoSuchFile
        : Directory.EnumerateFileSystemEntries(path).Any() ? NotEmpty
        : Done(() => Directory.Delete(path));

    // File::Copy's move renames a file or folder, replacing a file already at the target.
    private static int Move(string from, string to) =>
        File.Exists(from) ? Done(() => File.Move(from, to, overwrite: true))
        : Directory.Exists(from) ? Done(() => Directory.Move(from, to))
        : NoSuchFile;

    private static int Done(Action action)
    {
        action();
        return 0;
    }
}
