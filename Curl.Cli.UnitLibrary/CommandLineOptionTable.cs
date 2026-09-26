using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// The options <see cref="CommandLineParser"/> recognises, one <see cref="CommandLineOption"/> row each.
/// </summary>
/// <remarks>
/// To add an option, add one row here (with its applier, when it takes a value) and the
/// property it sets on <see cref="CommandLineOptions"/>; the parser does not change. A flag
/// that curl lets be turned off with <c>--no-&lt;name&gt;</c> opts in by being a
/// <see cref="CommandLineOption.NegatableFlag"/>; any other flag is <see cref="CommandLineOption.Flag"/>,
/// and its <c>--no-</c> spelling, like that of every value option, is refused with
/// <see cref="CommandLineRefusal.CannotBeReversed(string)"/>. Check the new row's <c>--no-</c>
/// spelling against a real curl before choosing. A text value is <see cref="CommandLineOption.Text"/>,
/// which refuses an empty value as blank; a file name is <see cref="CommandLineOption.FileName"/>,
/// which is <see cref="CommandLineOption.Text"/> plus curl's warning for a file name that looks
/// like a flag; a numeric value is <see cref="CommandLineOption.Value"/>
/// with an applier built on <see cref="CommandLineNumber"/>, which refuses an empty value as not
/// a proper number. The parser never refuses a value itself. Long
/// names are matched exactly and case-sensitively, as curl 8.21.0 does: <c>--sil</c> and
/// <c>--Silent</c> are both unknown (checked against the local curl 8.21.0 on 2026-09-26;
/// option list per <see href="https://curl.se/docs/manpage.html"/>). Each long name and
/// each short letter must appear in at most one row.
/// <para>
/// <c>--no-</c> negation, measured with the local curl 8.21.0 on 2026-09-26
/// (<c>curl &lt;arguments&gt; http://127.0.0.1:1/</c>, reading standard error and the exit code):
/// <c>--no-silent</c>, <c>--no-show-error</c>, <c>--no-insecure</c>, <c>--no-tftp-no-options</c>, <c>--no-remote-time</c>,
/// <c>--no-progress-meter</c>, <c>--no-progress-bar</c>, <c>--no-get</c>, <c>--no-location</c>, <c>--no-location-trusted</c>,
/// <c>--no-post301</c>, <c>--no-post302</c>, <c>--no-post303</c>, <c>--no-show-headers</c>, <c>--no-include</c>, <c>--no-head</c>,
/// <c>--no-fail</c>, <c>--no-fail-with-body</c> and <c>--no-fail-early</c> are accepted and turn their flag off; the last spelling wins, so <c>-s --no-silent</c> is not
/// silent and <c>--no-silent -s</c> is. <c>--no-silent=x</c> is accepted, its value ignored.
/// <c>--no-tlsv1.2</c>, <c>--no-tlsv1.3</c>, <c>--no-url</c>, <c>--no-output</c> (even as the last
/// argument), <c>--no-output=x</c>, <c>--no-data</c>, <c>--no-dump-header</c>, <c>--no-range</c>, <c>--no-time-cond</c>,
/// <c>--no-request</c>, <c>--no-header</c> (and <c>--no-header=x</c>), <c>--no-user-agent</c>, <c>--no-referer</c>,
/// <c>--no-data-ascii</c>, <c>--no-data-binary</c>, <c>--no-data-raw</c>, <c>--no-data-urlencode</c>, <c>--no-json</c>,
/// <c>--no-url-query</c> and <c>--no-max-redirs</c> (each also with <c>=x</c>) exit 2 with
/// <c>curl: option &lt;as typed&gt;: the given option cannot be reversed with a --no- prefix</c> and
/// the try-help line. <c>--no-bogus</c>, <c>--no-</c>, <c>--no-no-silent</c> and <c>--no-Silent</c>
/// exit 2 as unknown. A short letter is never negated.
/// </para>
/// </remarks>
public static class CommandLineOptionTable
{
    private static readonly CommandLineOption[] RowsInTableOrder =
    [
        CommandLineOption.Text("url", null, (options, url) => options.AddUrl(url)),
        CommandLineOption.NegatableFlag("silent", 's', (options, on) => options.Silent = on),
        CommandLineOption.NegatableFlag("show-error", 'S', (options, on) => options.ShowError = on),
        CommandLineOption.NegatableFlag("progress-meter", null, (options, on) => options.ProgressMeterOff = !on),
        CommandLineOption.NegatableFlag("progress-bar", '#', (options, on) => options.ProgressBar = on),
        CommandLineOption.FileName("output", 'o', (options, file) => options.AddOutputFile(file)),
        CommandLineOption.Value("data", 'd', AppendPostData),
        CommandLineOption.Value("data-ascii", null, AppendPostData),
        CommandLineOption.Value("data-binary", null, AppendBinaryPostData),
        CommandLineOption.Value("data-raw", null, AcceptingEmpty((options, data) => options.AppendPostData(data))),
        CommandLineOption.Value("data-urlencode", null, AppendUrlEncodedPostData),
        CommandLineOption.Value("json", null, AppendJsonData),
        CommandLineOption.NegatableFlag("get", 'G', (options, on) => options.DataInQuery = on),
        CommandLineOption.Value("url-query", null, AppendUrlQuery),
        CommandLineOption.FileName("dump-header", 'D', (options, file) => options.DumpHeaderFile = file),
        CommandLineOption.Value("user", 'u', AcceptingEmpty((options, user) => options.SetCredentials(user))),
        CommandLineOption.Value("telnet-option", 't', AcceptingEmpty((options, telnetOption) => options.AddTelnetOption(telnetOption))),
        CommandLineOption.Value("tftp-blksize", null, SetTftpBlockSize),
        CommandLineOption.NegatableFlag("tftp-no-options", null, (options, on) => options.TftpNoOptions = on),
        CommandLineOption.Value("create-file-mode", null, SetCreateFileMode),
        CommandLineOption.NegatableFlag("insecure", 'k', (options, on) => options.Insecure = on),
        CommandLineOption.Value("cacert", null, SetCaCertificateFile),
        CommandLineOption.FileName("capath", null, (options, directory) => options.CaCertificateDirectory = directory),
        CommandLineOption.FileName("cert", 'E', (options, certificate) => options.ClientCertificate = certificate),
        CommandLineOption.FileName("key", null, (options, key) => options.PrivateKey = key),
        CommandLineOption.Flag("tlsv1.2", null, options => options.MinimumTlsVersion = SslProtocols.Tls12),
        CommandLineOption.Flag("tlsv1.3", null, options => options.MinimumTlsVersion = SslProtocols.Tls13),
        CommandLineOption.Text("ciphers", null, (options, ciphers) => options.Ciphers = ciphers),
        CommandLineOption.Text("tls13-ciphers", null, (options, ciphers) => options.Tls13Ciphers = ciphers),
        CommandLineOption.Value("range", 'r', SetRange),
        CommandLineOption.Value("continue-at", 'C', SetResumeFrom),
        CommandLineOption.Value("max-filesize", null, SetMaxFileSize),
        CommandLineOption.Value("connect-timeout", null, SetConnectTimeout),
        CommandLineOption.Value("max-time", 'm', SetMaxTime),
        CommandLineOption.NegatableFlag("remote-time", 'R', (options, on) => options.RemoteTime = on),
        CommandLineOption.Value("time-cond", 'z', SetTimeCondition),
        CommandLineOption.Text("request", 'X', (options, method) => options.RequestMethod = method),
        CommandLineOption.Value("header", 'H', AddHeaders),
        CommandLineOption.Value("user-agent", 'A', AcceptingEmpty((options, userAgent) => options.UserAgent = userAgent)),
        CommandLineOption.Value("referer", 'e', AcceptingEmpty((options, referer) => options.Referer = referer)),
        CommandLineOption.NegatableFlag("location", 'L', (options, on) => options.FollowRedirects = on),
        CommandLineOption.NegatableFlag("location-trusted", null, SetLocationTrusted),
        CommandLineOption.Value("max-redirs", null, SetMaxRedirects),
        CommandLineOption.NegatableFlag("post301", null, (options, on) => options.KeepPostAfter301 = on),
        CommandLineOption.NegatableFlag("post302", null, (options, on) => options.KeepPostAfter302 = on),
        CommandLineOption.NegatableFlag("post303", null, (options, on) => options.KeepPostAfter303 = on),
        CommandLineOption.NegatableFlag("show-headers", 'i', (options, on) => options.ShowHeaders = on),
        CommandLineOption.NegatableFlag("include", null, (options, on) => options.ShowHeaders = on),
        CommandLineOption.NegatableFlagThatCanRefuse("head", 'I', SetHead),
        CommandLineOption.NegatableFlag("fail", 'f', SetFail),
        CommandLineOption.NegatableFlag("fail-with-body", null, SetFailWithBody),
        CommandLineOption.NegatableFlag("fail-early", null, (options, on) => options.FailEarly = on),
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
    /// <c>-u ''</c> and <c>-t ''</c>, and passes it to <paramref name="set"/>.
    /// </summary>
    private static CommandLineOptionApplier AcceptingEmpty(Action<CommandLineOptions, string> set) =>
        (options, value, _, _, _) =>
        {
            set(options, value);
            return null;
        };

    /// <summary>
    /// Appends a <c>-d</c> / <c>--data</c> or <c>--data-ascii</c> value to the body: the value's own text, empty included, or,
    /// when it starts with <c>@</c>, the bytes of the file it names (standard input for <c>@-</c>) with
    /// every carriage return, line feed and NUL byte removed, as curl 8.21.0 does. A file that cannot
    /// be read is refused with <see cref="CommandLineRefusal.DataFileUnreadable"/>.
    /// </summary>
    private static CommandLineRefusal? AppendPostData(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        if (!value.StartsWith('@'))
        {
            options.AppendPostData(value);
            return null;
        }

        CommandLineRefusal? refusal = ReadAtFile(options, value[1..], spelledOption, dataFileReader, out byte[] contents);
        if (refusal is null)
        {
            options.AppendPostData(RemoveLineBreaksAndNuls(contents));
        }

        return refusal;
    }

    /// <summary>
    /// Appends a <c>--data-binary</c> value to the body, after a <c>&amp;</c> when the body so far is not
    /// empty: the value's own text, or, when it starts with <c>@</c>, the bytes of the file it names
    /// (standard input for <c>@-</c>) unchanged, as curl 8.21.0 does.
    /// </summary>
    private static CommandLineRefusal? AppendBinaryPostData(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader) =>
        AppendTextOrFileBytes(options, value, spelledOption, dataFileReader, options.AppendPostData);

    /// <summary>
    /// Appends a <c>--json</c> value to the body with no separator and marks the request as JSON:
    /// the value's own text, or, when it starts with <c>@</c>, the bytes of the file it names
    /// (standard input for <c>@-</c>) unchanged, as curl 8.21.0 does.
    /// </summary>
    private static CommandLineRefusal? AppendJsonData(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader) =>
        AppendTextOrFileBytes(options, value, spelledOption, dataFileReader, options.AppendJsonData);

    /// <summary>
    /// Hands <paramref name="append"/> the UTF-8 bytes of <paramref name="value"/>, or, when it starts
    /// with <c>@</c>, the unchanged bytes of the file it names. A file that cannot be read is refused
    /// with <see cref="CommandLineRefusal.DataFileUnreadable"/> and nothing is appended.
    /// </summary>
    private static CommandLineRefusal? AppendTextOrFileBytes(CommandLineOptions options, string value, string spelledOption, IDataFileReader dataFileReader, Action<byte[]> append)
    {
        if (!value.StartsWith('@'))
        {
            append(Encoding.UTF8.GetBytes(value));
            return null;
        }

        CommandLineRefusal? refusal = ReadAtFile(options, value[1..], spelledOption, dataFileReader, out byte[] contents);
        if (refusal is null)
        {
            append(contents);
        }

        return refusal;
    }

    /// <summary>
    /// Appends a <c>--data-urlencode</c> value to the body, after a <c>&amp;</c> when the body so far
    /// is not empty, encoded by <see cref="UrlEncodeValue"/>.
    /// </summary>
    private static CommandLineRefusal? AppendUrlEncodedPostData(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineRefusal? refusal = UrlEncodeValue(options, value, spelledOption, dataFileReader, out string encoded);
        if (refusal is null)
        {
            options.AppendPostData(encoded);
        }

        return refusal;
    }

    /// <summary>
    /// Appends a <c>--url-query</c> value to <see cref="CommandLineOptions.UrlQuery"/>: the text after
    /// a leading <c>+</c> verbatim, or else the value encoded by <see cref="UrlEncodeValue"/>, as
    /// curl 8.21.0 does.
    /// </summary>
    private static CommandLineRefusal? AppendUrlQuery(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        if (value.StartsWith('+'))
        {
            options.AppendUrlQuery(value[1..]);
            return null;
        }

        CommandLineRefusal? refusal = UrlEncodeValue(options, value, spelledOption, dataFileReader, out string encoded);
        if (refusal is null)
        {
            options.AppendUrlQuery(encoded);
        }

        return refusal;
    }

    /// <summary>
    /// Encodes a <c>--data-urlencode</c> or <c>--url-query</c> value as curl 8.21.0 does. The value
    /// splits at its first <c>=</c>, or, when it holds none, at its first <c>@</c>. With neither, the
    /// whole value is the content. With <c>=</c>, the text after it is the content. With <c>@</c>, the
    /// content is the bytes of the file named after it (standard input for <c>-</c>), and an empty
    /// file makes the whole piece empty. The content is escaped by <see cref="UrlEncodedContent.Escape"/>
    /// and, when the text before the split is not empty, follows that text, unencoded, and a <c>=</c>.
    /// A file that cannot be read is refused with <see cref="CommandLineRefusal.DataFileUnreadable"/>.
    /// </summary>
    private static CommandLineRefusal? UrlEncodeValue(CommandLineOptions options, string value, string spelledOption, IDataFileReader dataFileReader, out string encoded)
    {
        int split = value.IndexOf('=', StringComparison.Ordinal);
        if (split < 0)
        {
            split = value.IndexOf('@', StringComparison.Ordinal);
        }

        if (split < 0)
        {
            encoded = UrlEncodedContent.Escape(Encoding.UTF8.GetBytes(value));
            return null;
        }

        string name = value[..split];
        if (value[split] == '=')
        {
            encoded = NameAndEscapedContent(name, Encoding.UTF8.GetBytes(value[(split + 1)..]));
            return null;
        }

        CommandLineRefusal? refusal = ReadAtFile(options, value[(split + 1)..], spelledOption, dataFileReader, out byte[] contents);
        encoded = contents.Length == 0 ? string.Empty : NameAndEscapedContent(name, contents);
        return refusal;
    }

    private static string NameAndEscapedContent(string name, byte[] content) =>
        name.Length == 0 ? UrlEncodedContent.Escape(content) : $"{name}={UrlEncodedContent.Escape(content)}";

    /// <summary>
    /// Adds a <c>-H</c> / <c>--header</c> value to the headers. A value that does not start with
    /// <c>@</c> is kept verbatim, empty included, after
    /// <see cref="CommandLineWarning.HeaderDoesNotLookLikeAHeader(string)"/> when it holds neither a
    /// colon nor a semicolon. A value that starts with <c>@</c> reads the file it names (standard
    /// input for <c>@-</c>) and adds each of its lines verbatim, splitting at every run of carriage
    /// returns and line feeds, so empty lines are skipped, and warning about none, as curl 8.21.0 does.
    /// A file that cannot be read is refused with <see cref="CommandLineRefusal.DataFileUnreadable"/>.
    /// </summary>
    private static CommandLineRefusal? AddHeaders(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        if (!value.StartsWith('@'))
        {
            if (!value.AsSpan().ContainsAny(':', ';'))
            {
                options.AddWarningLinesUnlessSilent([CommandLineWarning.HeaderDoesNotLookLikeAHeader(value)]);
            }

            options.AddHeader(value);
            return null;
        }

        CommandLineRefusal? refusal = ReadAtFile(options, value[1..], spelledOption, dataFileReader, out byte[] contents);
        if (refusal is null)
        {
            foreach (string line in Encoding.UTF8.GetString(contents).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                options.AddHeader(line);
            }
        }

        return refusal;
    }

    /// <summary>
    /// Reads the file an <c>@file</c> value names, or standard input when <paramref name="file"/> is
    /// <c>-</c>, and refuses a file that cannot be read with <see cref="CommandLineRefusal.DataFileUnreadable"/>.
    /// </summary>
    private static CommandLineRefusal? ReadAtFile(CommandLineOptions options, string file, string spelledOption, IDataFileReader dataFileReader, out byte[] contents)
    {
        if (file == "-")
        {
            contents = dataFileReader.ReadStandardInput();
            return null;
        }

        return dataFileReader.TryReadFile(file, out contents)
            ? null
            : CommandLineRefusal.DataFileUnreadable(spelledOption, file, options.ErrorsHidden);
    }

    private static byte[] RemoveLineBreaksAndNuls(byte[] contents) =>
        Array.FindAll(contents, octet => octet is not ((byte)'\r' or (byte)'\n' or 0));

    private static CommandLineRefusal? SetTftpBlockSize(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(spelledOption, value, out int blockSize);
        if (refusal is null)
        {
            options.TftpBlockSize = blockSize;
        }

        return refusal;
    }

    private static CommandLineRefusal? SetCreateFileMode(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
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
    /// A value that looks like a flag gets curl's filename warning first, whether or not it exists.
    /// </summary>
    private static CommandLineRefusal? SetCaCertificateFile(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineOption.WarnWhenFileNameLooksLikeFlag(options, value);
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
    private static CommandLineRefusal? SetRange(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
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
    private static CommandLineRefusal? SetResumeFrom(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
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

    private static CommandLineRefusal? SetMaxFileSize(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseSize(spelledOption, value, out long size);
        if (refusal is null)
        {
            options.MaxFileSize = size;
        }

        return refusal;
    }

    private static CommandLineRefusal? SetConnectTimeout(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseSeconds(spelledOption, value, out TimeSpan duration);
        if (refusal is null)
        {
            options.ConnectTimeout = duration;
        }

        return refusal;
    }

    private static CommandLineRefusal? SetMaxTime(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseSeconds(spelledOption, value, out TimeSpan duration);
        if (refusal is null)
        {
            options.MaxTime = duration;
        }

        return refusal;
    }

    /// <summary>
    /// Records a <c>-z</c>/<c>--time-cond</c> value as curl 8.21.0's tool reads it: a leading <c>-</c>
    /// asks for the resource only when it is not newer than the date
    /// (<see cref="TimeConditionKind.IfUnmodifiedSince"/>); a leading <c>+</c>, a leading <c>=</c> or
    /// none asks for it only when it is newer (<see cref="TimeConditionKind.IfModifiedSince"/>). The
    /// rest is read by <see cref="CurlDateParser"/>. A value that is not a date, empty included, is
    /// never refused: it clears any earlier condition and adds
    /// <see cref="CommandLineWarning.TimeConditionIsNotADate"/> unless <c>-s</c> / <c>--silent</c> has
    /// been read. curl first tries such a value as a file name whose modification time it uses; that
    /// is not done here yet.
    /// </summary>
    private static CommandLineRefusal? SetTimeCondition(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        TimeConditionKind kind = value.StartsWith('-') ? TimeConditionKind.IfUnmodifiedSince : TimeConditionKind.IfModifiedSince;
        string date = value.StartsWith('-') || value.StartsWith('+') || value.StartsWith('=') ? value[1..] : value;
        if (CurlDateParser.TryParse(date, out DateTimeOffset instant))
        {
            options.TimeCondition = new TimeCondition(instant, kind);
            return null;
        }

        options.TimeCondition = null;
        options.AddWarningLinesUnlessSilent(CommandLineWarning.TimeConditionIsNotADate);
        return null;
    }

    /// <summary>
    /// Turns <c>--location-trusted</c> on or off: it follows redirects and sends credentials to every
    /// host they lead to, and its <c>--no-</c> spelling turns off both, as curl 8.21.0's tool does.
    /// </summary>
    private static void SetLocationTrusted(CommandLineOptions options, bool on)
    {
        options.SendCredentialsToRedirectHosts = on;
        options.FollowRedirects = on;
    }

    private static CommandLineRefusal? SetMaxRedirects(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseMinusOneOrMore(spelledOption, value, out int limit);
        if (refusal is null)
        {
            options.MaxRedirects = limit;
        }

        return refusal;
    }

    /// <summary>
    /// Turns <c>-I</c> / <c>--head</c> on, which selects <c>HEAD</c> and shows the headers, or, for
    /// <c>--no-head</c>, off, which selects <c>GET</c> and hides them. Once one method is selected the
    /// other is refused with <see cref="CommandLineRefusal.BadlyUsedHere"/> after curl 8.21.0's two
    /// warning lines, which <c>-s</c> read before it drops.
    /// </summary>
    private static CommandLineRefusal? SetHead(CommandLineOptions options, bool on, string spelledOption)
    {
        SelectedHttpMethod method = on ? SelectedHttpMethod.Head : SelectedHttpMethod.Get;
        if (options.HttpMethodSelected != SelectedHttpMethod.None && options.HttpMethodSelected != method)
        {
            options.AddWarningLinesUnlessSilent(on ? CommandLineWarning.HeadRequestedAfterGet : CommandLineWarning.GetRequestedAfterHead);
            return CommandLineRefusal.BadlyUsedHere(spelledOption);
        }

        options.HttpMethodSelected = method;
        options.NoBody = on;
        options.ShowHeaders = on;
        return null;
    }

    /// <summary>
    /// Turns <c>-f</c> / <c>--fail</c> on, after <see cref="CommandLineWarning.FailDeselectsFailWithBody"/>
    /// when it replaces <c>--fail-with-body</c>, or, for <c>--no-fail</c>, turns off either fail mode.
    /// </summary>
    private static void SetFail(CommandLineOptions options, bool on) =>
        SetFailMode(options, on, HttpFailMode.Fail, HttpFailMode.FailWithBody, CommandLineWarning.FailDeselectsFailWithBody);

    /// <summary>
    /// Turns <c>--fail-with-body</c> on, after <see cref="CommandLineWarning.FailWithBodyDeselectsFail"/>
    /// when it replaces <c>-f</c> / <c>--fail</c>, or, for <c>--no-fail-with-body</c>, turns off either fail mode.
    /// </summary>
    private static void SetFailWithBody(CommandLineOptions options, bool on) =>
        SetFailMode(options, on, HttpFailMode.FailWithBody, HttpFailMode.Fail, CommandLineWarning.FailWithBodyDeselectsFail);

    private static void SetFailMode(CommandLineOptions options, bool on, HttpFailMode mode, HttpFailMode deselected, IReadOnlyList<string> deselectWarning)
    {
        if (on && options.FailMode == deselected)
        {
            options.AddWarningLinesUnlessSilent(deselectWarning);
        }

        options.FailMode = on ? mode : HttpFailMode.None;
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
