using System.Globalization;

namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>Writes the entries of a MIME run's result-code log, as the tester's <c>mimetest.py</c> did.</summary>
public static class LibcurlMimeLogWriter
{
    /// <summary>Writes one step's entry.</summary>
    /// <param name="kind">The call.</param>
    /// <param name="code">Its result code.</param>
    /// <returns>The entry, such as <c>fn=0</c>.</returns>
    public static string Step(LibcurlMimeStepKind kind, int code) =>
        string.Create(CultureInfo.InvariantCulture, $"{Label(kind)}={code}");

    /// <summary>Writes the entries of the <c>CURLOPT_MIMEPOST</c> call and the transfer.</summary>
    /// <param name="setOption">The <c>curl_easy_setopt</c> result code.</param>
    /// <param name="performed">The <c>curl_easy_perform</c> result code.</param>
    /// <returns>The entries, such as <c>setopt=0 rc=0</c>.</returns>
    public static string Transfer(int setOption, int performed) =>
        string.Create(CultureInfo.InvariantCulture, $"setopt={setOption} rc={performed}");

    private static string Label(LibcurlMimeStepKind kind) => kind switch
    {
        LibcurlMimeStepKind.Name => "name",
        LibcurlMimeStepKind.FileName => "fn",
        LibcurlMimeStepKind.Type => "type",
        LibcurlMimeStepKind.Encoder => "enc",
        LibcurlMimeStepKind.Data => "data",
        LibcurlMimeStepKind.File => "file",
        LibcurlMimeStepKind.Headers => "hdrs",
        _ => "sub",
    };
}
