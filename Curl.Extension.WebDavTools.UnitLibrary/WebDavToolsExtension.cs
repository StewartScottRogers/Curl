using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebDavTools.Sync;
using Curl.Extension.WebDavTools.Transfer;

namespace Curl.Extension.WebDavTools;

/// <summary>
/// The WebDAV tools extension (ADR-1164): <c>--webdav-list</c> lists a collection from a <c>PROPFIND</c> (RFC 4918
/// section 9.1) and <c>--webdav-sync &lt;dir&gt;</c> downloads a collection and every collection below it into a local
/// folder. <c>Curl.Extension.Composition</c> registers its options, description and transfer hook.
/// </summary>
public static class WebDavToolsExtension
{
    /// <summary>The message of a transfer refused because both options were given.</summary>
    public const string BothOptionsMessage = "--webdav-list and --webdav-sync cannot be used together";

    /// <summary>The message of a transfer refused because <c>--webdav-sync</c> was given an empty folder.</summary>
    public const string EmptyFolderMessage = "--webdav-sync expects a local folder";

    /// <summary>Gets the <c>--webdav-list</c> option.</summary>
    public static ExtensionOption ListOption { get; } = new(
        "webdav-list",
        ExtensionOptionArgument.None,
        "List a WebDAV collection's members from a PROPFIND, one per line");

    /// <summary>Gets the <c>--webdav-sync &lt;dir&gt;</c> option.</summary>
    public static ExtensionOption SyncOption { get; } = new(
        "webdav-sync",
        ExtensionOptionArgument.Value,
        "Download a WebDAV collection and its sub-collections into <dir>, skipping current files");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "webdav-tools",
        "Lists a WebDAV collection from a PROPFIND and syncs a collection tree into a local folder (RFC 4918).",
        [],
        [ListOption, SyncOption],
        new Uri("https://www.rfc-editor.org/rfc/rfc4918"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--webdav-list</c> or <c>--webdav-sync</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>
    /// The listing hook, the sync hook, a hook that refuses the transfer when both options or an empty folder were
    /// given, or <see langword="null" /> when neither option was given.
    /// </returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (values.ValueOf(SyncOption), values.IsGiven(ListOption)) switch
        {
            (null, false) => null,
            (null, true) => new WebDavListTransferHook(),
            (_, true) => new RefusedWebDavTransferHook(BothOptionsMessage),
            ({ Length: 0 }, _) => new RefusedWebDavTransferHook(EmptyFolderMessage),
            (var folder, _) => new WebDavSyncTransferHook(folder, new WebDavDiskFileSystem()),
        };
    }
}
