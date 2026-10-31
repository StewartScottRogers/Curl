using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.OpenApi;

/// <summary>
/// An OpenAPI 3 description read from JSON or YAML (OpenAPI 3.1 §4.3, §4.8.1), which finds an operation by its
/// <c>operationId</c> and resolves local <c>$ref</c>s (§4.8.23) with JSON Pointer (RFC 6901).
/// </summary>
internal sealed class OpenApiDocument
{
    private const int MaximumReferenceDepth = 32;
    private static readonly string[] Methods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];
    private readonly JsonElement root;

    private OpenApiDocument(JsonElement root) => this.root = root;

    /// <summary>Reads a description.</summary>
    /// <param name="bytes">
    /// The description file's bytes, UTF-8 with or without a byte order mark: JSON when the first character that is not
    /// white space is <c>{</c>, else YAML (§4.3), which <see cref="OpenApiYaml" /> converts to JSON.
    /// </param>
    /// <param name="path">The file's path, for messages.</param>
    /// <returns>The description.</returns>
    internal static OpenApiDocument Parse(byte[] bytes, string path)
    {
        ReadOnlyMemory<byte> text = bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? bytes.AsMemory(3) : bytes;
        ReadOnlyMemory<byte> json = text.Span.TrimStart(" \t\r\n"u8).StartsWith("{"u8) ? text : Yaml(text.Span, path);
        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw OpenApiFailure.Of($"{path} is not a JSON OpenAPI description");
        }

        string? version = OpenApiJson.StringMember(root, "openapi");
        return version is not null && version.StartsWith("3.", StringComparison.Ordinal)
            ? new(root)
            : throw OpenApiFailure.Of($"{path} is not an OpenAPI 3 description");
    }

    /// <summary>Finds the operation whose <c>operationId</c> is <paramref name="operationId" /> in <c>paths</c> (§4.8.8, §4.8.9).</summary>
    /// <param name="operationId">The operation's id.</param>
    /// <returns>The operation.</returns>
    internal OpenApiOperation FindOperation(string operationId)
    {
        foreach (JsonProperty path in OpenApiJson.ObjectMembers(root, "paths"))
        {
            JsonElement pathItem = Resolve(path.Value);
            foreach (string method in Methods)
            {
                JsonElement? operation = OpenApiJson.Member(pathItem, method);
                if (operation is { } found && OpenApiJson.StringMember(found, "operationId") == operationId)
                {
                    return new(operationId, method, path.Name, ServerUrl(pathItem, found), Parameters(pathItem, found), RequestBody(found));
                }
            }
        }

        throw OpenApiFailure.Of($"no operation {operationId} in the description");
    }

    /// <summary>Follows an element's <c>$ref</c>s, if it has any, to the element they name.</summary>
    /// <param name="element">The element, perhaps a Reference Object.</param>
    /// <returns>The element itself when it is no reference, else the one its reference chain ends at.</returns>
    internal JsonElement Resolve(JsonElement element)
    {
        for (int depth = 0; depth < MaximumReferenceDepth; depth++)
        {
            string? reference = OpenApiJson.StringMember(element, "$ref");
            if (reference is null)
            {
                return element;
            }

            element = Pointer(reference);
        }

        throw OpenApiFailure.Of("a $ref chain is circular or too long");
    }

    private static byte[] Yaml(ReadOnlySpan<byte> text, string path)
    {
        try
        {
            return OpenApiYaml.ToJson(Encoding.UTF8.GetString(text));
        }
        catch (NotSupportedException)
        {
            throw OpenApiFailure.Of($"{path} uses YAML anchors, aliases or tags, which --openapi does not read");
        }
        catch (FormatException)
        {
            throw OpenApiFailure.Of($"{path} is not a YAML OpenAPI description");
        }
    }

    private static bool TryStep(JsonElement element, string token, out JsonElement next)
    {
        next = default;
        if (element.ValueKind == JsonValueKind.Array)
        {
            bool isIndex = int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index < element.GetArrayLength();
            next = isIndex ? element[index] : default;
            return isIndex;
        }

        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(token, out next);
    }

    private JsonElement Pointer(string reference)
    {
        if (!reference.StartsWith('#'))
        {
            throw OpenApiFailure.Of($"$ref {reference} is not in the description; only local references are read");
        }

        JsonElement current = root;
        foreach (string token in reference[1..].Split('/').Skip(1))
        {
            if (!TryStep(current, Uri.UnescapeDataString(token).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal), out current))
            {
                throw OpenApiFailure.Of($"$ref {reference} does not resolve");
            }
        }

        return current;
    }

    private string ServerUrl(JsonElement pathItem, JsonElement operation)
    {
        foreach (JsonElement source in new[] { operation, pathItem, root })
        {
            JsonElement server = OpenApiJson.ArrayItems(source, "servers").FirstOrDefault();
            if (server.ValueKind == JsonValueKind.Object)
            {
                return OpenApiJson.ObjectMembers(server, "variables").Aggregate(
                    OpenApiJson.StringMember(server, "url") ?? "/",
                    (url, variable) => url.Replace($"{{{variable.Name}}}", OpenApiJson.StringMember(variable.Value, "default") ?? string.Empty, StringComparison.Ordinal));
            }
        }

        return "/";
    }

    private List<OpenApiParameter> Parameters(JsonElement pathItem, JsonElement operation)
    {
        Dictionary<(string Name, string In), OpenApiParameter> parameters = [];
        foreach (JsonElement element in OpenApiJson.ArrayItems(pathItem, "parameters").Concat(OpenApiJson.ArrayItems(operation, "parameters")))
        {
            OpenApiParameter parameter = OpenApiParameter.From(Resolve(element));
            parameters[(parameter.Name, parameter.In)] = parameter;
        }

        return [.. parameters.Values.Where(parameter => !parameter.IsIgnored)];
    }

    private OpenApiRequestBody? RequestBody(JsonElement operation)
    {
        if (OpenApiJson.Member(operation, "requestBody") is not { } body)
        {
            return null;
        }

        List<JsonProperty> content = [.. OpenApiJson.ObjectMembers(Resolve(body), "content")];
        if (content.Count == 0)
        {
            return null;
        }

        JsonProperty media = content.FirstOrDefault(candidate => OpenApiRequestBody.IsSupported(candidate.Name), content[0]);
        return new(media.Name, Properties(media.Value));
    }

    private Dictionary<string, OpenApiPropertyType> Properties(JsonElement media)
    {
        JsonElement schema = OpenApiJson.Member(media, "schema") is { } found ? Resolve(found) : default;
        return OpenApiJson.ObjectMembers(schema, "properties").ToDictionary(property => property.Name, property => PropertyType(Resolve(property.Value)), StringComparer.Ordinal);
    }

    private OpenApiPropertyType PropertyType(JsonElement schema) =>
        new(TypeOf(schema), OpenApiJson.Member(schema, "items") is { } items ? TypeOf(Resolve(items)) : null);

    private static string? TypeOf(JsonElement schema) =>
        OpenApiJson.StringMember(schema, "type")
        ?? OpenApiJson.ArrayItems(schema, "type").Select(type => type.ValueKind == JsonValueKind.String ? type.GetString() : null).FirstOrDefault(type => type is not null and not "null");
}
