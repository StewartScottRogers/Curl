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

    /// <summary>
    /// Gets what reads a handle's own <c>.netrc</c> file (<c>CURLOPT_NETRC</c>, <c>CURLOPT_NETRC_FILE</c>): the path's
    /// bytes, or <see langword="null" /> when it cannot be read. The real disk (<see cref="ReadDiskFile" />) by default.
    /// </summary>
    internal Func<string, byte[]?> ReadFile { get; init; } = ReadDiskFile;

    /// <summary>
    /// Gets where a handle's <c>CURLOPT_VERBOSE</c> lines go when neither <c>CURLOPT_DEBUGFUNCTION</c> nor
    /// <c>CURLOPT_STDERR</c> is set, as libcurl writes them to the process's <c>stderr</c> (BL-2726): the process's
    /// standard error by default, the conformance harness's case stderr for a ported libtest program.
    /// </summary>
    internal Lazy<Stream> StandardError { get; init; } = new(Console.OpenStandardError);

    /// <summary>Reads a file from the disk whole.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>Its bytes, or <see langword="null" /> when it is missing, a directory or not readable.</returns>
    internal static byte[]? ReadDiskFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
