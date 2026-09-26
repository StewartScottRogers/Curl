using System.Diagnostics.CodeAnalysis;

namespace Curl.Cli;

/// <summary>
/// The outcome of <see cref="CommandLineParser.Parse"/>: either the parsed
/// <see cref="CommandLineOptions"/> or the <see cref="CommandLineRefusal"/> that stopped parsing.
/// </summary>
public sealed class CommandLineParseResult
{
    private CommandLineParseResult(CommandLineOptions? options, CommandLineRefusal? refusal)
    {
        Options = options;
        Refusal = refusal;
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

    /// <summary>Creates the result for an accepted command line.</summary>
    /// <param name="options">The parsed options.</param>
    /// <returns>A result whose <see cref="IsAccepted"/> is <see langword="true"/>.</returns>
    internal static CommandLineParseResult Accepted(CommandLineOptions options) => new(options, null);

    /// <summary>Creates the result for a refused command line.</summary>
    /// <param name="refusal">The first refusal met.</param>
    /// <returns>A result whose <see cref="IsAccepted"/> is <see langword="false"/>.</returns>
    internal static CommandLineParseResult Refused(CommandLineRefusal refusal) => new(null, refusal);
}
