using System.Text.Json;

namespace Curl.Extension.OpenApi;

/// <summary>One Parameter Object of an operation (OpenAPI 3.1 §4.8.12), with its serialization defaults filled in.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="In">Where it goes: <c>path</c>, <c>query</c>, <c>header</c> or <c>cookie</c>.</param>
/// <param name="Style">How its value is serialized (§4.8.12.4).</param>
/// <param name="Explode">Whether an array gives one name and value per item.</param>
/// <param name="Required">Whether the operation needs it.</param>
/// <param name="AllowReserved">Whether a query value keeps RFC 3986 reserved characters unencoded.</param>
internal sealed record OpenApiParameter(string Name, string In, string Style, bool Explode, bool Required, bool AllowReserved)
{
    private static readonly string[] IgnoredHeaders = ["Accept", "Content-Type", "Authorization"];

    /// <summary>Gets a value indicating whether the definition is ignored: a header parameter named <c>Accept</c>, <c>Content-Type</c> or <c>Authorization</c> (§4.8.12.1).</summary>
    internal bool IsIgnored => In == "header" && IgnoredHeaders.Contains(Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads a resolved Parameter Object.</summary>
    /// <param name="element">The Parameter Object, its <c>$ref</c> already resolved.</param>
    /// <returns>The parameter; <c>style</c> defaults to <c>form</c> in a query or cookie and <c>simple</c> elsewhere, <c>explode</c> to whether the style is <c>form</c>, and <c>required</c> to whether it is in the path (§4.8.12.1, §4.8.12.3).</returns>
    internal static OpenApiParameter From(JsonElement element)
    {
        string name = OpenApiJson.StringMember(element, "name") ?? throw OpenApiFailure.Of("a parameter has no name");
        string location = OpenApiJson.StringMember(element, "in") ?? throw OpenApiFailure.Of($"parameter {name} has no in");
        string style = OpenApiJson.StringMember(element, "style") ?? DefaultStyle(location);
        return new(name, location, style, Flag(element, "explode", style == "form"), Flag(element, "required", location == "path"), Flag(element, "allowReserved", false));
    }

    private static string DefaultStyle(string location) => location is "query" or "cookie" ? "form" : "simple";

    private static bool Flag(JsonElement element, string name, bool fallback) => OpenApiJson.BooleanMember(element, name) ?? fallback;
}
