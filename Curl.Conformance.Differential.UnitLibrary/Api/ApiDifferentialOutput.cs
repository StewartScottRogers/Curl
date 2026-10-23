using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Api;

/// <summary>
/// Writes what a <c>Curl.Api</c> call did as the bytes its equivalent curl command line writes to
/// standard output with <see cref="ApiDifferentialCase.LeadingArguments"/>: the body, a newline and
/// the response code as <c>%{response_code}</c> prints it (three digits, <c>000</c> for none).
/// </summary>
public static class ApiDifferentialOutput
{
    /// <summary>Gets the standard output for <paramref name="body"/> and <paramref name="responseCode"/>, 0 when no response came.</summary>
    public static byte[] Render(ReadOnlySpan<byte> body, int responseCode) =>
        [.. body, .. Encoding.ASCII.GetBytes("\n" + responseCode.ToString("D3", CultureInfo.InvariantCulture))];
}
