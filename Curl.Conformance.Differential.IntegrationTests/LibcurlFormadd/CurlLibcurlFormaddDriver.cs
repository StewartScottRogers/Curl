using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Mime;
using Curl.Conformance.Differential.Libcurl.Formadd;
using Curl.Conformance.Differential.Libcurl.Mime;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlFormadd;

// curl_formadd and CURLOPT_HTTPPOST are deprecated, as upstream's are.
#pragma warning disable CS0618

/// <summary>
/// Runs a formadd case through Curl's Libcurl: each <c>curl_formadd</c> call, then, when the case
/// sends, <c>curl_formget</c> and a <c>CURLOPT_HTTPPOST</c> post, logging every result code.
/// </summary>
internal static class CurlLibcurlFormaddDriver
{
    /// <summary>Runs <paramref name="formCase"/> against <paramref name="url"/>.</summary>
    /// <returns>The result-code log, entries separated by spaces, and the bytes <c>curl_formget</c> gave.</returns>
    public static (string Log, byte[] FormGet) Run(CurlLibrary library, LibcurlFormaddCase formCase, string url, string directory)
    {
        var log = new List<string>();
        CurlForm? form = null;
        foreach (var call in formCase.Calls)
        {
            log.Add(LibcurlFormaddLogWriter.Add((int)CurlForm.Add(library, ref form, [.. call.Select(a => Argument(a, directory))])));
        }

        var formGet = new MemoryStream();
        if (formCase.Sends)
        {
            log.Add(form is null ? LibcurlFormaddLogWriter.NoForm : LibcurlFormaddLogWriter.Get(form.Get(piece => { formGet.Write(piece); return piece.Length; })));
            using var easy = library.EasyInit();
            WriteCallback discard = data => data.Length;
            easy.SetOption(CurlOption.Writefunction, discard);
            easy.SetOption(CurlOption.Url, url);
            var setOption = (int)easy.SetOption(CurlOption.Httppost, form);
            log.Add(LibcurlMimeLogWriter.Transfer(setOption, (int)easy.Perform()));
        }

        CurlForm.Free(form);
        return (string.Join(' ', log), formGet.ToArray());
    }

    private static CurlFormArgument Argument(LibcurlFormaddArgument argument, string directory) =>
        new((CurlFormOption)argument.Option, ValueOf(argument, directory));

    private static object? ValueOf(LibcurlFormaddArgument argument, string directory) => argument.Kind switch
    {
        LibcurlFormaddValueKind.Text => argument.Text,
        LibcurlFormaddValueKind.Bytes => Convert.FromHexString(argument.Text!),
        LibcurlFormaddValueKind.Length => argument.Number,
        LibcurlFormaddValueKind.File => Path.Combine(directory, argument.Text!),
        LibcurlFormaddValueKind.Headers => argument.Lines,
        LibcurlFormaddValueKind.Array => argument.Array.Select(a => Argument(a, directory)).ToList(),
        LibcurlFormaddValueKind.Pointer => new object(),
        _ => null,
    };
}
