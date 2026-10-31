namespace Curl.Extension.OpenApi;

/// <summary>The JSON Schema type of one request body property (OpenAPI 3.1 §4.8.24), so its value is written as that type.</summary>
/// <param name="Type">The property's <c>type</c>, or <see langword="null" /> when the schema gives none.</param>
/// <param name="ItemType">The <c>type</c> of an array property's <c>items</c>, or <see langword="null" />.</param>
internal sealed record OpenApiPropertyType(string? Type, string? ItemType)
{
    /// <summary>Gets the type of a property the schema does not name: its value is written as a string.</summary>
    internal static OpenApiPropertyType Untyped { get; } = new(null, null);
}
