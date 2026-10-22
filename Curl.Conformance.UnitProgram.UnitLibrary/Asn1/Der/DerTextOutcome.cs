using System.Text;
using Curl.Api.Codes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Der;

/// <summary>
/// What one call of a <c>lib/vtls/x509asn1.c</c> printer returns in the DER ports: the <c>CURLcode</c>
/// and, on success, the text it left in the C program's <c>dynbuf</c>, as UTF-8 bytes.
/// </summary>
/// <param name="Code"><see cref="CurlCode.Ok"/>, or the code the printer failed with.</param>
/// <param name="Text">The printed bytes; empty when <paramref name="Code"/> is not <see cref="CurlCode.Ok"/>.</param>
public readonly record struct DerTextOutcome(CurlCode Code, byte[] Text)
{
    /// <summary>
    /// Runs <paramref name="print"/> into a <c>dynbuf</c> made with <c>curlx_dyn_init(&amp;dbuf, toobig)</c>:
    /// a <see cref="DerTextException"/> is its code, and text that with its terminating NUL is longer than
    /// <paramref name="bufferLimit"/> is <see cref="CurlCode.TooLarge"/>, as <c>curlx_dyn_addn</c> refuses it.
    /// </summary>
    /// <param name="print">The Curl printer under test.</param>
    /// <param name="bufferLimit">The C program's <c>toobig</c>.</param>
    /// <returns>The outcome.</returns>
    public static DerTextOutcome Print(Func<string> print, int bufferLimit)
    {
        ArgumentNullException.ThrowIfNull(print);
        byte[] text;
        try
        {
            text = Encoding.UTF8.GetBytes(print());
        }
        catch (DerTextException exception)
        {
            return new DerTextOutcome(exception.Code, []);
        }

        return text.Length + 1 > bufferLimit ? new DerTextOutcome(CurlCode.TooLarge, []) : new DerTextOutcome(CurlCode.Ok, text);
    }
}
