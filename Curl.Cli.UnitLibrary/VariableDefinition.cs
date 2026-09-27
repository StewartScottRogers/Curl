using System.Text;

namespace Curl.Cli;

/// <summary>
/// Reads a <c>--variable</c> value as curl 8.21.0's <c>setvariable</c> does and sets the variable on
/// <see cref="CommandLineOptions"/>: <c>name=content</c>, <c>name@file</c> or <c>name@-</c> (standard input),
/// optionally with a <c>[start-end]</c> or <c>[start-]</c> byte range after the name, and <c>%name</c> to
/// import the environment variable <c>name</c>, which must be set unless <c>=</c> or <c>@</c> gives a
/// default. An imported value is never cut to the byte range. A name that is empty or 128 bytes or longer,
/// and a value that is neither of these forms, are skipped with a warning; a malformed or backwards range
/// is refused, and so is a file that cannot be opened.
/// </summary>
/// <remarks>
/// Measured with the local curl 8.21.0 on 2026-09-27 (through <c>--expand-data</c> against a loopback
/// server, from PowerShell, since Git Bash rewrites some arguments) and checked against <c>src/var.c</c>
/// at tag <c>curl-8_21_0</c>. The environment is this process's; see ADR-0063.
/// </remarks>
internal static class VariableDefinition
{
    private const char ImportMarker = '%';

    private const string StandardInputName = "-";

    /// <summary>Reports whether <paramref name="name"/> holds only ASCII letters, digits and underscores, as a variable name must.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when every character is allowed in a name.</returns>
    internal static bool IsName(string name) => NameLength(name, 0) == name.Length;

    /// <summary>Applies a <c>--variable</c> value.</summary>
    /// <param name="options">The options being filled in.</param>
    /// <param name="value">The value as given.</param>
    /// <param name="spelledOption">The option as typed, for a refusal.</param>
    /// <param name="pathExists">The parse's path-existence check, used to word a file that cannot be opened.</param>
    /// <param name="dataFileReader">Reads the file, or standard input, the value names.</param>
    /// <returns><see langword="null"/> when the value was applied or skipped; otherwise the refusal.</returns>
    internal static CommandLineRefusal? Apply(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        bool imports = value.StartsWith(ImportMarker);
        if (!TryReadName(options, value, imports ? 1 : 0, out string name, out string rest))
        {
            return null;
        }

        string? imported = imports ? Environment.GetEnvironmentVariable(name) : null;
        return ImportIsMissing(imports, imported, rest)
            ? CommandLineRefusal.VariableExpansionFailure(spelledOption, $"Variable '{name}' import fail, not set", options.ErrorsHidden)
            : ApplyAfterName(options, name, rest, imported, (value, spelledOption, pathExists, dataFileReader));
    }

    /// <summary>
    /// Reports whether an import has nothing to set: <c>%name</c> alone, with no <c>=</c> or <c>@</c> default,
    /// for an environment variable that is not set, which curl refuses.
    /// </summary>
    private static bool ImportIsMissing(bool imports, string? imported, string rest) =>
        imports && imported is null && rest.Length == 0;

    /// <summary>
    /// Reads the name starting at <paramref name="nameStart"/>, or warns, as curl does, that its length
    /// (zero, or 128 bytes and more) makes it skip the variable.
    /// </summary>
    private static bool TryReadName(CommandLineOptions options, string value, int nameStart, out string name, out string rest)
    {
        int nameLength = NameLength(value, nameStart);
        name = value.Substring(nameStart, nameLength);
        rest = value[(nameStart + nameLength)..];
        if (nameLength == 0 || nameLength >= VariableExpansion.MaximumNameLength)
        {
            options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", $"Bad variable name length ({nameLength}), skipping"));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the byte range after the name, then sets the variable to the imported environment value when
    /// there is one, or else to what follows the range.
    /// </summary>
    private static CommandLineRefusal? ApplyAfterName(CommandLineOptions options, string name, string rest, string? imported, (string Value, string SpelledOption, Func<string, bool> PathExists, IDataFileReader DataFileReader) source)
    {
        if (!VariableByteRange.TryRead(ref rest, out VariableByteRange range))
        {
            return CommandLineRefusal.VariableSyntaxError(source.SpelledOption);
        }

        return imported is not null
            ? Set(options, name, Encoding.UTF8.GetBytes(imported))
            : ApplyContent(options, name, rest, range, source);
    }

    /// <summary>Sets the variable from what follows its name and range: <c>@file</c>, <c>@-</c> or <c>=content</c>; skips it with a warning otherwise.</summary>
    private static CommandLineRefusal? ApplyContent(CommandLineOptions options, string name, string rest, VariableByteRange range, (string Value, string SpelledOption, Func<string, bool> PathExists, IDataFileReader DataFileReader) source)
    {
        if (rest.StartsWith('@'))
        {
            return ReadFile(options, name, rest[1..], range, source.SpelledOption, source.PathExists, source.DataFileReader);
        }

        if (rest.StartsWith('='))
        {
            return Set(options, name, range.Cut(Encoding.UTF8.GetBytes(rest[1..])));
        }

        options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", $"Bad --variable syntax, skipping: {source.Value}"));
        return null;
    }

    private static CommandLineRefusal? ReadFile(CommandLineOptions options, string name, string file, VariableByteRange range, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        if (file == StandardInputName)
        {
            return Set(options, name, range.Cut(dataFileReader.ReadStandardInput()));
        }

        return dataFileReader.TryReadFile(file, out byte[] contents)
            ? Set(options, name, range.Cut(contents))
            : CommandLineRefusal.VariableFileUnreadable(spelledOption, file, OpenFailureReason(file, pathExists), options.ErrorsHidden);
    }

    /// <summary>
    /// The <c>strerror</c> text the Windows curl 8.21.0 prints after <c>Failed to open &lt;file&gt;: </c>,
    /// measured 2026-09-27: <c>Invalid argument</c> for the empty name, <c>Permission denied</c> for a path
    /// that exists but cannot be read (a directory), and <c>No such file or directory</c> otherwise.
    /// </summary>
    private static string OpenFailureReason(string file, Func<string, bool> pathExists)
    {
        if (file.Length == 0)
        {
            return "Invalid argument";
        }

        return pathExists(file) ? "Permission denied" : "No such file or directory";
    }

    private static CommandLineRefusal? Set(CommandLineOptions options, string name, byte[] content)
    {
        options.SetVariable(name, content);
        return null;
    }

    /// <summary>Counts the name characters (ASCII letters, digits, underscores) in <paramref name="text"/> from <paramref name="start"/>.</summary>
    private static int NameLength(string text, int start)
    {
        int end = start;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
        {
            end++;
        }

        return end - start;
    }
}
