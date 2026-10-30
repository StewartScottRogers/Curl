using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.RecordReplay;

/// <summary>
/// Names the request a transfer sends (ADR-1129 section 2): its method, and its key, the lowercase hexadecimal
/// SHA-256 of the Latin-1 line <c>&lt;method&gt; &lt;url&gt;</c> and an LF followed by the <c>-d</c> body bytes.
/// </summary>
public static class ExchangeKey
{
    /// <summary>
    /// Gives the method the transfer sends: <c>-X</c>'s when given, else <c>HEAD</c> for <c>-I</c>, <c>POST</c> for
    /// a <c>-d</c> or <c>-F</c> body, <c>PUT</c> for <c>-T</c>, and <c>GET</c> otherwise.
    /// </summary>
    public static string MethodOf(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Http?.CustomMethod ?? ImpliedMethodOf(context);
    }

    /// <summary>Gives the key of the request <paramref name="method" /> sends to <paramref name="url" /> with <paramref name="body" />.</summary>
    public static string For(string method, string url, ReadOnlySpan<byte> body)
    {
        byte[] line = Encoding.Latin1.GetBytes(method + " " + url + "\n");
        return Convert.ToHexStringLower(SHA256.HashData([.. line, .. body]));
    }

    private static string ImpliedMethodOf(ITransferContext context) =>
        context.NoBody ? "HEAD"
        : SendsBody(context) ? "POST"
        : context.Upload is not null ? "PUT"
        : "GET";

    private static bool SendsBody(ITransferContext context) =>
        context.PostData is not null || context.PostWithoutData || context.Http?.Body is not null;
}
