using Curl.Api.Callbacks;
using Curl.Conformance.Differential.Libcurl.Mime;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMime;

/// <summary>
/// Runs a MIME case through Curl's Libcurl as the tester's <c>LibTest</c> MIME mode did: builds
/// the structure step by step, posts it with <c>CURLOPT_MIMEPOST</c> and logs every result code.
/// </summary>
internal static class CurlLibcurlMimeDriver
{
    /// <summary>Runs <paramref name="mimeCase"/> against <paramref name="url"/>.</summary>
    /// <returns>The result-code log, entries separated by spaces.</returns>
    public static string Run(CurlLibrary library, LibcurlMimeCase mimeCase, string url, string directory)
    {
        using var easy = library.EasyInit();
        var log = new List<string>();
        WriteCallback discard = data => data.Length;
        easy.SetOption(CurlOption.Writefunction, discard);
        easy.SetOption(CurlOption.Url, url);
        var mime = library.MimeInit();
        Fill(library, mime, mimeCase.Parts, log, directory);
        var setOption = (int)easy.SetOption(CurlOption.Mimepost, mime);
        log.Add(LibcurlMimeLogWriter.Transfer(setOption, (int)easy.Perform()));
        return string.Join(' ', log);
    }

    private static void Fill(CurlLibrary library, CurlMime mime, IReadOnlyList<LibcurlMimePart> parts, List<string> log, string directory)
    {
        foreach (var part in parts)
        {
            var added = mime.AddPart();
            foreach (var step in part.Steps)
            {
                log.Add(LibcurlMimeLogWriter.Step(step.Kind, (int)Apply(library, added, step, log, directory)));
            }
        }
    }

    private static Curl.Api.Codes.CurlCode Apply(CurlLibrary library, CurlMimePart part, LibcurlMimeStep step, List<string> log, string directory) => step.Kind switch
    {
        LibcurlMimeStepKind.Name => part.SetName(step.Text),
        LibcurlMimeStepKind.FileName => part.SetFileName(step.Text),
        LibcurlMimeStepKind.Type => part.SetType(step.Text),
        LibcurlMimeStepKind.Encoder => part.SetEncoder(step.Text),
        LibcurlMimeStepKind.Data => part.SetData(step.Text is null ? null : Convert.FromHexString(step.Text)),
        LibcurlMimeStepKind.File => part.SetFileData(Path.Combine(directory, step.Text!)),
        LibcurlMimeStepKind.Headers => part.SetHeaders(step.Headers),
        _ => part.SetSubParts(SubParts(library, step, log, directory)),
    };

    private static CurlMime SubParts(CurlLibrary library, LibcurlMimeStep step, List<string> log, string directory)
    {
        var sub = library.MimeInit();
        Fill(library, sub, step.SubParts, log, directory);
        return sub;
    }
}
