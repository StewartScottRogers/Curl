namespace Curl.Protocol.Pop3;

/// <summary>
/// The server's answer to <c>CAPA</c> (RFC 2449): every line of it up to the lone <c>.</c>
/// that ends it, the <c>+OK</c> status line included, as curl 8.21.0 reads it.
/// </summary>
/// <param name="Lines">Each line without its line end, such as <c>STLS</c> and <c>SASL PLAIN LOGIN</c>.</param>
internal sealed record Pop3Capabilities(IReadOnlyList<string> Lines)
{
    private const string StlsKeyword = "STLS";

    /// <summary>
    /// Gets a value indicating whether a line starts with <c>STLS</c> in any case, as
    /// measured on curl 8.21.0: <c>stls</c> and <c>STLSX</c> count, <c>XSTLS</c> does not.
    /// </summary>
    public bool AdvertisesStls =>
        Lines.Any(line => line.StartsWith(StlsKeyword, StringComparison.OrdinalIgnoreCase));
}
