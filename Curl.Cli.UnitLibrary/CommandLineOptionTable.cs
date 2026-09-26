using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Authentication;

namespace Curl.Cli;

/// <summary>
/// The options <see cref="CommandLineParser"/> recognises, one <see cref="CommandLineOption"/> row each.
/// </summary>
/// <remarks>
/// To add an option, add one row here (with its applier, when it takes a value) and the
/// property it sets on <see cref="CommandLineOptions"/>; the parser does not change. A flag
/// is <see cref="CommandLineOption.Flag"/>; a text value is <see cref="CommandLineOption.Text"/>,
/// which refuses an empty value as blank; a file name is <see cref="CommandLineOption.FileName"/>,
/// which is <see cref="CommandLineOption.Text"/> plus curl's warning for a file name that looks
/// like a flag; a numeric value is <see cref="CommandLineOption.Value"/>
/// with an applier built on <see cref="CommandLineNumber"/>, which refuses an empty value as not
/// a proper number. The parser never refuses a value itself. Long
/// names are matched exactly and case-sensitively, as curl 8.21.0 does: <c>--sil</c> and
/// <c>--Silent</c> are both unknown (checked against the local curl 8.21.0 on 2026-09-26;
/// option list per <see href="https://curl.se/docs/manpage.html"/>). Each long name and
/// each short letter must appear in at most one row.
/// </remarks>
public static class CommandLineOptionTable
{
    private static readonly CommandLineOption[] RowsInTableOrder =
    [
        CommandLineOption.Text("url", null, (options, url) => options.AddUrl(url)),
        CommandLineOption.Flag("silent", 's', options => options.Silent = true),
        CommandLineOption.Flag("show-error", 'S', options => options.ShowError = true),
        CommandLineOption.FileName("output", 'o', (options, file) => options.AddOutputFile(file)),
        CommandLineOption.Value("data", 'd', AcceptingEmpty((options, data) => options.SetPostData(data))),
        CommandLineOption.Value("user", 'u', AcceptingEmpty((options, user) => options.SetCredentials(user))),
        CommandLineOption.Value("telnet-option", 't', AcceptingEmpty((options, telnetOption) => options.AddTelnetOption(telnetOption))),
        CommandLineOption.Value("tftp-blksize", null, SetTftpBlockSize),
        CommandLineOption.Flag("tftp-no-options", null, options => options.TftpNoOptions = true),
        CommandLineOption.Value("create-file-mode", null, SetCreateFileMode),
        CommandLineOption.Flag("insecure", 'k', options => options.Insecure = true),
        CommandLineOption.Value("cacert", null, SetCaCertificateFile),
        CommandLineOption.Text("capath", null, (options, directory) => options.CaCertificateDirectory = directory),
        CommandLineOption.Text("cert", 'E', (options, certificate) => options.ClientCertificate = certificate),
        CommandLineOption.Text("key", null, (options, key) => options.PrivateKey = key),
        CommandLineOption.Flag("tlsv1.2", null, options => options.MinimumTlsVersion = SslProtocols.Tls12),
        CommandLineOption.Flag("tlsv1.3", null, options => options.MinimumTlsVersion = SslProtocols.Tls13),
        CommandLineOption.Text("ciphers", null, (options, ciphers) => options.Ciphers = ciphers),
        CommandLineOption.Text("tls13-ciphers", null, (options, ciphers) => options.Tls13Ciphers = ciphers),
        CommandLineOption.Value("range", 'r', SetRange),
        CommandLineOption.Value("continue-at", 'C', SetResumeFrom),
        CommandLineOption.Value("max-filesize", null, SetMaxFileSize),
    ];

    /// <summary>The largest <c>--create-file-mode</c> curl 8.21.0 accepts: octal <c>0777</c>.</summary>
    private const int MaximumCreateFileMode = 0b111_111_111;

    /// <summary>The characters curl 8.21.0 expects in a range, and warns about any other.</summary>
    private static readonly SearchValues<char> RangeCharacters = SearchValues.Create("0123456789-,");

    private static readonly FrozenDictionary<string, CommandLineOption> RowsByLongName =
        RowsInTableOrder.ToFrozenDictionary(option => option.LongName, StringComparer.Ordinal);

    private static readonly FrozenDictionary<char, CommandLineOption> RowsByShortName =
        RowsInTableOrder.Where(option => option.ShortName.HasValue).ToFrozenDictionary(option => option.ShortName!.Value);

    /// <summary>
    /// Every row of the table, in table order. These are the option definitions, not the
    /// parsed settings; those are <see cref="CommandLineOptions"/>.
    /// </summary>
    public static IReadOnlyList<CommandLineOption> Rows => RowsInTableOrder;

    /// <summary>
    /// Builds an applier that accepts any value, empty included, as curl 8.21.0 does for
    /// <c>-d ''</c>, <c>-u ''</c> and <c>-t ''</c>, and passes it to <paramref name="set"/>.
    /// </summary>
    private static CommandLineOptionApplier AcceptingEmpty(Action<CommandLineOptions, string> set) =>
        (options, value, _, _) =>
        {
            set(options, value);
            return null;
        };

    private static CommandLineRefusal? SetTftpBlockSize(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(spelledOption, value, out int blockSize);
        if (refusal is null)
        {
            options.TftpBlockSize = blockSize;
        }

        return refusal;
    }

    private static CommandLineRefusal? SetCreateFileMode(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseOctal(spelledOption, value, MaximumCreateFileMode, out int mode);
        if (refusal is null)
        {
            options.CreateFileMode = (UnixFileMode)mode;
        }

        return refusal;
    }

    /// <summary>
    /// Records a <c>--cacert</c> value when a file or directory exists at it, and otherwise refuses
    /// it with curl 8.21.0's three lines. An empty value is checked like any other, so it is refused
    /// as a missing file, not as blank. A directory passes here; curl fails it later, at handshake.
    /// </summary>
    private static CommandLineRefusal? SetCaCertificateFile(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists)
    {
        if (!pathExists(value))
        {
            return CommandLineRefusal.FileDoesNotExist(spelledOption, "--cacert", value);
        }

        options.CaCertificateFile = value;
        return null;
    }

    /// <summary>
    /// Records a <c>-r</c>/<c>--range</c> value the way curl 8.21.0 keeps it. It is refused when
    /// <c>-C</c>/<c>--continue-at</c> came first (checked before anything else, so
    /// <c>-C 5 -r ''</c> is that refusal, not a blank one) and when empty. A value that starts
    /// with a digit and has no dash becomes its leading number with a dash appended, with a
    /// warning, unless that number does not fit, when it is kept verbatim without one; any other
    /// value holding anything but digits, dashes and commas is kept verbatim with a warning.
    /// Parsing the text into a range is <c>ByteRangeParser</c>'s job, at transfer time.
    /// </summary>
    private static CommandLineRefusal? SetRange(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists)
    {
        if (options.ResumeFrom is not null || options.ResumeFromOutputSize)
        {
            return CommandLineRefusal.ContinueAtExclusiveWithRange(spelledOption, options.ErrorsHidden);
        }

        if (value.Length == 0)
        {
            return CommandLineRefusal.BlankArgument(spelledOption);
        }

        options.Range = char.IsAsciiDigit(value[0]) && !value.Contains('-', StringComparison.Ordinal)
            ? AppendDashToLeadingNumber(options, value)
            : WarnOfInvalidRangeCharacter(options, value);
        return null;
    }

    private static string AppendDashToLeadingNumber(CommandLineOptions options, string value)
    {
        int digitCount = value.AsSpan().IndexOfAnyExceptInRange('0', '9');
        ReadOnlySpan<char> digits = digitCount < 0 ? value : value.AsSpan(0, digitCount);
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long firstBytePosition))
        {
            return value;
        }

        options.AddWarningLinesUnlessSilent(CommandLineWarning.RangeHasNoDash);
        return firstBytePosition.ToString(CultureInfo.InvariantCulture) + "-";
    }

    private static string WarnOfInvalidRangeCharacter(CommandLineOptions options, string value)
    {
        if (value.AsSpan().ContainsAnyExcept(RangeCharacters))
        {
            options.AddWarningLinesUnlessSilent(CommandLineWarning.RangeHasInvalidCharacter);
        }

        return value;
    }

    /// <summary>
    /// Records a <c>-C</c>/<c>--continue-at</c> value: <c>-</c> for "from the output file's size",
    /// or a byte offset read by <see cref="CommandLineNumber.ParseOffset"/>. It is refused when
    /// <c>-r</c>/<c>--range</c> came first, before the value is looked at, as curl 8.21.0 does.
    /// </summary>
    private static CommandLineRefusal? SetResumeFrom(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists)
    {
        if (options.Range is not null)
        {
            return CommandLineRefusal.ContinueAtExclusiveWithRange(spelledOption, options.ErrorsHidden);
        }

        if (value == "-")
        {
            options.ResumeFrom = null;
            options.ResumeFromOutputSize = true;
            return null;
        }

        CommandLineRefusal? refusal = CommandLineNumber.ParseOffset(spelledOption, value, out long offset);
        if (refusal is null)
        {
            options.ResumeFrom = offset;
            options.ResumeFromOutputSize = false;
        }

        return refusal;
    }

    private static CommandLineRefusal? SetMaxFileSize(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseSize(spelledOption, value, out long size);
        if (refusal is null)
        {
            options.MaxFileSize = size;
        }

        return refusal;
    }

    /// <summary>Finds the row whose long name is exactly <paramref name="longName"/>; no prefix matching.</summary>
    /// <param name="longName">The name without its leading <c>--</c> and without any <c>=value</c>.</param>
    /// <param name="option">The row found; <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a row was found.</returns>
    internal static bool TryFindLong(string longName, [NotNullWhen(true)] out CommandLineOption? option) =>
        RowsByLongName.TryGetValue(longName, out option);

    /// <summary>Finds the row whose short letter is <paramref name="shortName"/>, case-sensitively.</summary>
    /// <param name="shortName">The letter after <c>-</c>, or one letter of a bundle.</param>
    /// <param name="option">The row found; <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a row was found.</returns>
    internal static bool TryFindShort(char shortName, [NotNullWhen(true)] out CommandLineOption? option) =>
        RowsByShortName.TryGetValue(shortName, out option);
}
