using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RecordReplay;

/// <summary>
/// The <c>--record</c> and <c>--replay</c> extension (ADR-1135): records each HTTP exchange to a directory in the
/// Curl exchange record format 1, and answers transfers from such a directory without the network.
/// <c>Curl.Extension.Composition</c> registers its options, description and transfer hook.
/// </summary>
public static class RecordReplayExtension
{
    /// <summary>Gets the <c>--record &lt;dir&gt;</c> option.</summary>
    public static ExtensionOption RecordOption { get; } = new(
        "record",
        ExtensionOptionArgument.Value,
        "Record each HTTP exchange to <dir>, one file per request, for --replay");

    /// <summary>Gets the <c>--replay &lt;dir&gt;</c> option.</summary>
    public static ExtensionOption ReplayOption { get; } = new(
        "replay",
        ExtensionOptionArgument.Value,
        "Answer HTTP requests from the exchanges --record wrote to <dir>, without the network");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "record-replay",
        "Records HTTP exchanges to a directory and replays them later without the network (Curl exchange record format 1, ADR-1135).",
        [],
        [RecordOption, ReplayOption],
        new Uri("https://github.com/StewartScottRogers/Curl/blob/master/Documentation/Planning/Decisions/ADR-1135-record-and-replay-store-exchanges-in-curl-exchange-record-format-1.md"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--record</c> or <c>--replay</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook over the named directories, or <see langword="null" /> when neither option was given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var record = values.ValueOf(RecordOption);
        var replay = values.ValueOf(ReplayOption);
        return record is null && replay is null
            ? null
            : new RecordReplayTransferHook(DirectoryOrNone(replay), DirectoryOrNone(record));
    }

    private static ExchangeDirectory? DirectoryOrNone(string? path) => path is null ? null : new ExchangeDirectory(path);
}
