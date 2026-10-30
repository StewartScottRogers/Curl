using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PluginProtocol;

/// <summary>
/// The helper program extension's description and handler factory (ADR-1129): a URL whose scheme no handler
/// serves runs through a program named <c>curl-proto-&lt;scheme&gt;</c> on <c>PATH</c>.
/// </summary>
public static class PluginProtocolExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "plugin-protocol",
        "Hands a URL whose scheme neither curl nor another extension serves to a program named curl-proto-<scheme> on PATH (PATHEXT on Windows), as git hands remotes to git-remote-<transport>: the helper gets the URL as its argument, lists its capabilities (get, put) on stdout, and answers get <url> or put <url> (the -T or -d data follows on stdin) with ok and the body, or error <message>. Without such a program the scheme fails as curl fails one it does not support.",
        [],
        [],
        new Uri("https://git-scm.com/docs/gitremote-helpers"));

    /// <summary>Gets curl's own schemes, every one upstream curl can be built with; a helper never takes one of them.</summary>
    public static IReadOnlyList<string> CurlSchemes { get; } =
    [
        "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ipfs", "ipns", "ldap",
        "ldaps", "mqtt", "mqtts", "pop3", "pop3s", "rtmp", "rtmpe", "rtmps", "rtmpt", "rtmpte", "rtmpts", "rtsp", "scp",
        "sftp", "smb", "smbs", "smtp", "smtps", "telnet", "tftp", "ws", "wss",
    ];

    /// <summary>Creates the handler for the helper programs on this process's <c>PATH</c>.</summary>
    /// <param name="services">Curl's connection services; a helper opens its own connections, so none is used.</param>
    /// <param name="extensionSchemes">The schemes the other extensions serve, which a helper never takes either.</param>
    /// <returns>The handler, serving one scheme per helper found.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services, IEnumerable<string> extensionSchemes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(extensionSchemes);
        return Create(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows(), HelperProgramDirectory.ListHelperFiles, new SystemHelperProcessStarter(), extensionSchemes);
    }

    /// <summary>Creates the handler for the helpers <paramref name="listHelperFiles" /> finds in the <c>PATH</c> directories.</summary>
    /// <param name="readEnvironmentVariable">Reads <c>PATH</c> and <c>PATHEXT</c>.</param>
    /// <param name="isWindows">Whether <c>PATH</c> is <c>;</c>-separated and a program needs a <c>PATHEXT</c> extension.</param>
    /// <param name="listHelperFiles">Lists one directory's <c>curl-proto-*</c> files.</param>
    /// <param name="starter">Starts a helper.</param>
    /// <param name="extensionSchemes">The schemes the other extensions serve.</param>
    /// <returns>The handler.</returns>
    internal static HelperProtocolHandler Create(
        Func<string, string?> readEnvironmentVariable,
        bool isWindows,
        Func<string, IEnumerable<string>> listHelperFiles,
        IHelperProcessStarter starter,
        IEnumerable<string> extensionSchemes)
    {
        var search = new HelperProgramSearch(readEnvironmentVariable("PATH"), readEnvironmentVariable("PATHEXT"), isWindows);
        return new HelperProtocolHandler(search.Find(listHelperFiles, [.. CurlSchemes, .. extensionSchemes]), starter);
    }
}
