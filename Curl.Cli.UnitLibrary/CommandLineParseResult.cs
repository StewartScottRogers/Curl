using System.Diagnostics.CodeAnalysis;

namespace Curl.Cli;

/// <summary>
/// The outcome of <see cref="CommandLineParser"/>: either the parsed
/// <see cref="CommandLineOptions"/> or the <see cref="CommandLineRefusal"/> that stopped parsing,
/// and in either case the warning lines met on the way.
/// </summary>
public sealed class CommandLineParseResult
{
    private CommandLineParseResult(CommandLineOptions? options, CommandLineRefusal? refusal, IReadOnlyList<string> warningLines, IReadOnlyList<string> warningLinesAfterTransfers)
    {
        Options = options;
        Refusal = refusal;
        WarningLines = warningLines;
        WarningLinesAfterTransfers = warningLinesAfterTransfers;
    }

    /// <summary>
    /// <see langword="true"/> when the command line was accepted and <see cref="Options"/> is set;
    /// <see langword="false"/> when it was refused and <see cref="Refusal"/> is set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Options))]
    [MemberNotNullWhen(false, nameof(Refusal))]
    public bool IsAccepted => Options is not null;

    /// <summary>The parsed options; <see langword="null"/> when the command line was refused.</summary>
    public CommandLineOptions? Options { get; }

    /// <summary>The first refusal met; <see langword="null"/> when the command line was accepted.</summary>
    public CommandLineRefusal? Refusal { get; }

    /// <summary>
    /// The warning lines curl prints on standard error while reading the command line, in
    /// command-line order and without line terminators; empty when there are none. A refused
    /// command line keeps the warnings met before the refusal, because curl 8.21.0 has already
    /// printed them by then (<c>-o -s --bogus</c> prints the warning, then the refusal). The
    /// console layer writes them before anything else and chooses the newline.
    /// </summary>
    public IReadOnlyList<string> WarningLines { get; }

    /// <summary>
    /// The warning lines curl prints on standard error about the command line after the last
    /// transfer has ended, without line terminators; empty when there are none, and always empty
    /// for a refused command line, which never reaches a transfer. It holds
    /// <see cref="CommandLineWarning.MoreOutputOptionsThanUrls"/> when an accepted command line has
    /// an <c>-o</c>, <c>-O</c> or kept <c>--no-remote-name</c> with no URL to pair with (an entry of
    /// <see cref="CommandLineOptions.UrlOutputs"/> with no URL)
    /// and <c>-s</c> / <c>--silent</c> is not in effect at its end. The console layer must write
    /// these after the transfers, not with <see cref="WarningLines"/>: curl 8.21.0 prints
    /// <c>curl -o f -o g file:///Z:/nx</c> as <c>curl: (37) Could not open file Z:/nx</c> and then
    /// the warning (measured on Windows on 2026-09-26).
    /// </summary>
    public IReadOnlyList<string> WarningLinesAfterTransfers { get; }

    /// <summary>
    /// Creates the result for an accepted command line, with the warning lines its options
    /// collected and the ones curl prints after the transfers.
    /// </summary>
    /// <param name="options">The parsed options.</param>
    /// <returns>A result whose <see cref="IsAccepted"/> is <see langword="true"/>.</returns>
    internal static CommandLineParseResult Accepted(CommandLineOptions options)
    {
        IReadOnlyList<string> warningLinesAfterTransfers =
            options.HasMoreOutputOptionsThanUrls && !options.Silent
                ? [CommandLineWarning.MoreOutputOptionsThanUrls]
                : [];
        return new(options, null, options.WarningLines, warningLinesAfterTransfers);
    }

    /// <summary>
    /// Creates the result for a command line whose parsing <c>-V</c> / <c>--version</c>, <c>-M</c> /
    /// <c>--manual</c> or <c>-h</c> / <c>--help</c> ended: accepted, with the warning lines met before it
    /// and none after the transfers, because there are none.
    /// </summary>
    /// <param name="options">The options read up to and including the option that asked.</param>
    /// <returns>
    /// A result whose <see cref="IsAccepted"/> is <see langword="true"/> and whose options ask for the
    /// version, the manual or help.
    /// </returns>
    internal static CommandLineParseResult InformationRequested(CommandLineOptions options) =>
        new(options, null, options.WarningLines, []);

    /// <summary>Creates the result for a refused command line.</summary>
    /// <param name="refusal">The first refusal met.</param>
    /// <param name="warningLines">The warning lines met before the refusal.</param>
    /// <returns>A result whose <see cref="IsAccepted"/> is <see langword="false"/>.</returns>
    internal static CommandLineParseResult Refused(CommandLineRefusal refusal, IReadOnlyList<string> warningLines) =>
        new(null, refusal, warningLines, []);
}
