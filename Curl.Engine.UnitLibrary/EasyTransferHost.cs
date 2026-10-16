using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What a <see cref="ManagedEasyTransfer" /> reaches outside the engine for its per-transfer setup and
/// cleanup (ADR-0544): the file system the <c>-b</c> files, the <c>-c</c> jar, the <c>--alt-svc</c> file and
/// the <c>--hsts</c> file are read and written through, what <c>-b -</c> reads, where <c>-c -</c> prints the
/// jar, the diagnostic log every transfer's context carries, and the platform's line endings for the
/// <c>--hsts</c> file. <c>Curl.Console</c> sets its own on <see cref="ManagedEasyTransfer.Host" /> once its
/// runner's serial attempts are built from the easy options (BL-2394); every other handle takes <see cref="Library" />.
/// </summary>
/// <param name="FileSystem">Reads and writes the cookie, alt-svc and HSTS files.</param>
/// <param name="StandardInput">What a <c>-b -</c> file reads.</param>
/// <param name="JarPrintTarget">Gives the stream <c>-c -</c> prints the jar to, asked for only when it prints.</param>
/// <param name="DiagnosticLog">The diagnostic log every transfer's context carries.</param>
/// <param name="RunsOnWindows">Whether the <c>--hsts</c> file's lines end in CR LF.</param>
/// <param name="AltSvcUsesOverHttp">Whether the alt-svc cache also serves plain <c>http</c> origins (<c>CURL_ALTSVC_HTTP</c>).</param>
internal sealed record EasyTransferHost(
    IFileSystem FileSystem,
    Stream StandardInput,
    Func<Stream> JarPrintTarget,
    IDiagnosticLog DiagnosticLog,
    bool RunsOnWindows,
    bool AltSvcUsesOverHttp)
{
    /// <summary>
    /// Gets the host of a <c>Curl.Api</c> or <c>Libcurl</c> handle: the real disk, as libcurl reads
    /// <c>CURLOPT_COOKIEFILE</c> and writes <c>CURLOPT_COOKIEJAR</c>, <c>CURLOPT_ALTSVC</c> and
    /// <c>CURLOPT_HSTS</c>; nothing to read for <c>-</c> and nowhere to print the jar to, as the engine
    /// writes to no standard stream of its own (ADR-0542); no diagnostic log; and the running platform's line
    /// endings.
    /// </summary>
    internal static EasyTransferHost Library { get; } = new(
        new PhysicalFileSystem(),
        Stream.Null,
        () => Stream.Null,
        NoDiagnosticLog.Instance,
        OperatingSystem.IsWindows(),
        AltSvcUsesOverHttp: false);
}
