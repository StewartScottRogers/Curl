using Curl.Api.EngineContracts.Url;

namespace Curl.Mcp;

/// <summary>
/// The parts of one URL as the <c>parse_url</c> tool returns them and the <c>build_url</c> tool takes
/// them: libcurl's URL API parts (<c>CURLUPART_*</c>) in its order (ADR-0548). A part that is
/// <see langword="null" /> is absent: not in the parsed URL, or not supplied to be set.
/// </summary>
/// <param name="Scheme">The scheme, such as <c>https</c>.</param>
/// <param name="User">The user name.</param>
/// <param name="Password">The password.</param>
/// <param name="Options">The login options (IMAP, POP3 and SMTP).</param>
/// <param name="Host">The host name or address.</param>
/// <param name="Port">The port number, as text.</param>
/// <param name="Path">The path.</param>
/// <param name="Query">The query, without its <c>?</c>.</param>
/// <param name="Fragment">The fragment, without its <c>#</c>.</param>
/// <param name="ZoneId">The IPv6 zone identifier.</param>
public sealed record McpUrlParts(
    string? Scheme = null,
    string? User = null,
    string? Password = null,
    string? Options = null,
    string? Host = null,
    string? Port = null,
    string? Path = null,
    string? Query = null,
    string? Fragment = null,
    string? ZoneId = null)
{
    /// <summary>The parts in libcurl's order, each with the name the tools give it.</summary>
    internal static IReadOnlyList<(CurlUrlPart Part, string Name)> InOrder { get; } =
    [
        (CurlUrlPart.Scheme, "scheme"),
        (CurlUrlPart.User, "user"),
        (CurlUrlPart.Password, "password"),
        (CurlUrlPart.Options, "options"),
        (CurlUrlPart.Host, "host"),
        (CurlUrlPart.Port, "port"),
        (CurlUrlPart.Path, "path"),
        (CurlUrlPart.Query, "query"),
        (CurlUrlPart.Fragment, "fragment"),
        (CurlUrlPart.ZoneId, "zoneId"),
    ];

    /// <summary>The parts from their values in <see cref="InOrder" />'s order.</summary>
    /// <param name="values">Ten values, <see langword="null" /> for each absent part.</param>
    /// <returns>The parts.</returns>
    internal static McpUrlParts FromValues(IReadOnlyList<string?> values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9]);

    /// <summary>The values of the parts in <see cref="InOrder" />'s order, <see langword="null" /> for each absent one.</summary>
    /// <returns>Ten values.</returns>
    internal string?[] Values() => [Scheme, User, Password, Options, Host, Port, Path, Query, Fragment, ZoneId];
}
