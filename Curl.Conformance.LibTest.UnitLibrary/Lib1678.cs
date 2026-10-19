using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Misc.Version;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1678.c</c>: where the library lists <c>SSLS-EXPORT</c> (<c>USE_SSLS_EXPORT</c>, the OpenSSL build Curl
/// matches off Windows), one easy handle on a share handle sharing <c>CURL_LOCK_DATA_SSL_SESSION</c> imports
/// <see cref="Packet"/> under <c>example.test:443</c> <see cref="ImportCount"/> times with <c>curl_easy_ssls_import</c>,
/// and the first import not answering <c>CURLE_OK</c> writes <c>import N failed: CODE (TEXT)</c> on stderr and ends
/// the program with its code. A handle that cannot be created, or a share or setopt that fails, ends it with
/// <c>CURLE_FAILED_INIT</c> or the setopt's code and writes nothing. Where the library lists no <c>SSLS-EXPORT</c>
/// (the Schannel build Curl matches on Windows), the program is upstream's build without it: it succeeds at once.
/// </summary>
public sealed class Lib1678 : ILibTest
{
    /// <summary><c>T1678_IMPORT_COUNT</c>: how many times the packet is imported.</summary>
    public const int ImportCount = 64;

    /// <summary>The peer key every import names.</summary>
    public const string SessionKey = "example.test:443";

    /// <summary>
    /// <c>t1678_make_packet</c>'s packet: <c>CURL_SPACK_VERSION</c> 1, a 4096-byte ticket of <c>A</c>, a 127-byte QUIC
    /// transport parameter block of <c>Q</c> and a 10-byte ALPN of <c>a</c>, then a one-byte ticket <c>B</c>, ALPN
    /// <c>b</c> and QUIC block <c>R</c>.
    /// </summary>
    public static IReadOnlyList<byte> Packet { get; } = MakePacket();

    /// <inheritdoc/>
    public string ProgramName => "lib1678";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LibTestRun.WithLibraryAsync(context, (steps, library) =>
        {
            if (CurlVersion.Info(library).FeatureNames.Contains("SSLS-EXPORT"))
            {
                Test(context, steps, library);
            }

            return ValueTask.CompletedTask;
        });
    }

    /// <summary>The program's body as built with <c>USE_SSLS_EXPORT</c>, after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps; <see cref="LibTestSteps.Result"/> becomes the program's <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Test(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        CurlShare? share = library.IsCleanedUp ? null : library.ShareInit();
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        steps.Result = ShareThenImport(context, easy, share);
        easy?.Dispose();
        _ = share?.Cleanup();
    }

    // CURLE_FAILED_INIT unless both handles exist and the share takes CURL_LOCK_DATA_SSL_SESSION.
    private static CurlCode ShareThenImport(LibTestContext context, CurlEasy? easy, CurlShare? share) =>
        share is not null && easy is not null && share.SetOption(CurlShareOption.Share, CurlLockData.SslSession) == CurlShareCode.Ok
            ? ImportOverShare(context, easy, share)
            : CurlCode.FailedInit;

    /// <summary>
    /// <c>curl_easy_setopt(easy, CURLOPT_SHARE, share)</c>, ending with its code when it fails, then the import loop:
    /// <see cref="ImportCount"/> imports, stopping at the first that fails with its line.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="easy">The handle importing.</param>
    /// <param name="share">The <c>CURLOPT_SHARE</c> value, a <see cref="CurlShare"/> in the program.</param>
    /// <returns><see cref="CurlCode.Ok"/>, the setopt's code, or the first failing import's code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="easy"/> is <see langword="null"/>.</exception>
    public static CurlCode ImportOverShare(LibTestContext context, CurlEasy easy, object? share)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(easy);
        CurlCode set = easy.SetOption(CurlOption.Share, share);
        return set == CurlCode.Ok ? ImportAll(context, easy) : set;
    }

    private static CurlCode ImportAll(LibTestContext context, CurlEasy easy)
    {
        byte[] packet = [.. Packet];
        for (int i = 0; i < ImportCount; i++)
        {
            CurlCode code = easy.SslsImport(SessionKey, null, packet);
            if (code != CurlCode.Ok)
            {
                context.StandardError.Write($"import {i} failed: {(int)code} ({CurlErrorText.Easy(code)})\n");
                return code;
            }
        }

        return CurlCode.Ok;
    }

    private static byte[] MakePacket()
    {
        var packet = new List<byte> { 0x01 };
        AddEntry(packet, 0x04, 'A', 4096);
        AddEntry(packet, 0x07, 'Q', 127);
        AddEntry(packet, 0x05, 'a', 10);
        AddEntry(packet, 0x04, 'B', 1);
        AddEntry(packet, 0x05, 'b', 1);
        AddEntry(packet, 0x07, 'R', 1);
        return [.. packet];
    }

    // One tag, its 16-bit big-endian length, then that many copies of the fill byte.
    private static void AddEntry(List<byte> packet, byte tag, char fill, int length)
    {
        packet.Add(tag);
        packet.Add((byte)(length >> 8));
        packet.Add((byte)(length & 0xFF));
        packet.AddRange(Enumerable.Repeat((byte)fill, length));
    }
}
