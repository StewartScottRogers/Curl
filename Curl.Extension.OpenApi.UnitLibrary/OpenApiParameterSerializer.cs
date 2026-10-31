namespace Curl.Extension.OpenApi;

/// <summary>
/// Serializes a parameter's values by its <c>style</c> and <c>explode</c> (OpenAPI 3.1 §4.8.12.4, Style Values and
/// Style Examples). One value is a primitive; a name given more than once is an array.
/// </summary>
internal static class OpenApiParameterSerializer
{
    /// <summary>Gives the parameter as it goes in its place: a path segment, a query string piece, a header value or a cookie pair.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="values">Its values, in command-line order; at least one.</param>
    /// <returns>The serialized text.</returns>
    internal static string Serialize(OpenApiParameter parameter, IReadOnlyList<string> values) => parameter.In switch
    {
        "path" => Path(parameter, Encoded(values, false)),
        "query" => Query(parameter, Encoded(values, parameter.AllowReserved)),
        _ => Unencoded(parameter, values),
    };

    private static string[] Encoded(IReadOnlyList<string> values, bool allowReserved) =>
        [.. values.Select(value => OpenApiPercentEncoding.Encode(value, allowReserved))];

    private static string Unencoded(OpenApiParameter parameter, IReadOnlyList<string> values) => (parameter.In, parameter.Style) switch
    {
        ("header", "simple") => string.Join(',', values),
        ("cookie", "form") => Pairs(parameter, parameter.Name, values, "; "),
        _ => throw Unsupported(parameter),
    };

    private static string Path(OpenApiParameter parameter, IReadOnlyList<string> values) => parameter.Style switch
    {
        "simple" => string.Join(',', values),
        "label" => "." + string.Join(parameter.Explode ? '.' : ',', values),
        "matrix" when parameter.Explode => string.Concat(values.Select(value => $";{parameter.Name}={value}")),
        "matrix" => $";{parameter.Name}={string.Join(',', values)}",
        _ => throw Unsupported(parameter),
    };

    private static string Query(OpenApiParameter parameter, IReadOnlyList<string> values)
    {
        string name = OpenApiPercentEncoding.Encode(parameter.Name, parameter.AllowReserved);
        return parameter.Style switch
        {
            "form" => Pairs(parameter, name, values, "&"),
            "spaceDelimited" when !parameter.Explode => $"{name}={string.Join("%20", values)}",
            "pipeDelimited" when !parameter.Explode => $"{name}={string.Join('|', values)}",
            "spaceDelimited" or "pipeDelimited" => Pairs(parameter, name, values, "&"),
            _ => throw Unsupported(parameter),
        };
    }

    private static string Pairs(OpenApiParameter parameter, string name, IReadOnlyList<string> values, string separator) =>
        parameter.Explode ? string.Join(separator, values.Select(value => $"{name}={value}")) : $"{name}={string.Join(',', values)}";

    private static Abstractions.Arguments.ArgumentExpansionException Unsupported(OpenApiParameter parameter) =>
        OpenApiFailure.Of($"parameter {parameter.Name} in {parameter.In} has style {parameter.Style}, which is not supported there");
}
