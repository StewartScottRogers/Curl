using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Formadd;

/// <summary>Writes the entries of a formadd run's result-code log and joins its <c>curl_formget</c> output to its wire log.</summary>
public static class LibcurlFormaddLogWriter
{
    /// <summary>The entry for a case whose calls left no form to read.</summary>
    public const string NoForm = "get=none";

    /// <summary>Writes one <c>curl_formadd</c> call's entry.</summary>
    /// <param name="code">Its <c>CURLFORMcode</c>.</param>
    /// <returns>The entry, such as <c>add=0</c>.</returns>
    public static string Add(int code) => string.Create(CultureInfo.InvariantCulture, $"add={code}");

    /// <summary>Writes the <c>curl_formget</c> call's entry.</summary>
    /// <param name="code">Its result.</param>
    /// <returns>The entry, such as <c>get=0</c>.</returns>
    public static string Get(int code) => string.Create(CultureInfo.InvariantCulture, $"get={code}");

    /// <summary>Joins what <c>curl_formget</c> gave and the loopback server's log of the post, so one comparison covers both.</summary>
    /// <param name="formGet">The bytes <c>curl_formget</c> handed its callback.</param>
    /// <param name="wire">The loopback server's log.</param>
    /// <returns><c>formget:</c>, a line feed, the bytes, a line feed, <c>wire:</c>, a line feed and the log.</returns>
    public static byte[] Join(byte[] formGet, byte[] wire)
    {
        ArgumentNullException.ThrowIfNull(formGet);
        ArgumentNullException.ThrowIfNull(wire);
        return [.. Encoding.ASCII.GetBytes("formget:\n"), .. formGet, .. Encoding.ASCII.GetBytes("\nwire:\n"), .. wire];
    }
}
