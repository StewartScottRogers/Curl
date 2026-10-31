namespace Curl.Extension.PrivacyPass.Wire;

/// <summary>The base64url encoding of RFC 4648 §5 that RFC 9577 §2.1.1 and §2.2.1 put its parameters in.</summary>
internal static class Base64Url
{
    /// <summary>Encodes <paramref name="bytes"/> without padding.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The base64url text.</returns>
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes <paramref name="text"/>, with or without padding.</summary>
    /// <param name="text">The base64url text.</param>
    /// <returns>The bytes, or <see langword="null"/> when the text is not base64url.</returns>
    public static byte[]? Decode(string text)
    {
        var base64 = text.TrimEnd('=').Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        var bytes = new byte[base64.Length];
        return text.AsSpan().IndexOfAny('+', '/') < 0 && Convert.TryFromBase64String(base64, bytes, out var written) ? bytes[..written] : null;
    }
}
