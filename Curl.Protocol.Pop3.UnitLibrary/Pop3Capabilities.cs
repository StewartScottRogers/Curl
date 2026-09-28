namespace Curl.Protocol.Pop3;

/// <summary>
/// The server's answer to <c>CAPA</c> (RFC 2449): every line of it up to the lone <c>.</c>
/// that ends it, the <c>+OK</c> status line included, as curl 8.21.0 reads it.
/// </summary>
/// <param name="Lines">Each line without its line end, such as <c>STLS</c> and <c>SASL PLAIN LOGIN</c>.</param>
internal sealed record Pop3Capabilities(IReadOnlyList<string> Lines)
{
    private const string StlsKeyword = "STLS";

    private const string UserKeyword = "USER";

    private const string SaslKeyword = "SASL";

    /// <summary>
    /// Gets a value indicating whether a line starts with <c>STLS</c> in any case, as
    /// measured on curl 8.21.0: <c>stls</c> and <c>STLSX</c> count, <c>XSTLS</c> does not.
    /// </summary>
    public bool AdvertisesStls =>
        Lines.Any(line => line.StartsWith(StlsKeyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a value indicating whether a line starts with <c>USER</c> in any case, which lets
    /// the session log in with <c>USER</c> and <c>PASS</c> (<c>user</c> measured in BL-548).
    /// </summary>
    public bool AdvertisesUser =>
        Lines.Any(line => line.StartsWith(UserKeyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the SASL mechanisms (RFC 5034 section 3) every <c>SASL</c> line lists, the keyword
    /// in any case (<c>sasl plain</c> measured in BL-548), in the order listed; empty when
    /// none is listed.
    /// </summary>
    public IReadOnlyList<string> SaslMechanisms =>
    [
        .. Lines
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(words => words.Length > 0 && words[0].Equals(SaslKeyword, StringComparison.OrdinalIgnoreCase))
            .SelectMany(words => words.Skip(1)),
    ];
}
