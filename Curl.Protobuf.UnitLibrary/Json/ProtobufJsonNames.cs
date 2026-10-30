using System.Text;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>The JSON names of fields under the proto3 JSON mapping.</summary>
public static class ProtobufJsonNames
{
    /// <summary>The field's <c>json_name</c> option, or else its name in lowerCamelCase: underscores dropped and the letter after each made upper case.</summary>
    public static string JsonName(DynamicField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.Descriptor.Options.FirstOrDefault(option => option.Name == "json_name")?.Value.Trim('"', '\'') ?? LowerCamelCase(field.Name);
    }

    /// <summary>The field of the type a JSON member names, by its JSON name or its original name; null when none.</summary>
    public static DynamicField? FindField(DynamicMessageType type, string memberName)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.Fields.FirstOrDefault(field => JsonName(field) == memberName || field.Name == memberName);
    }

    private static string LowerCamelCase(string name)
    {
        var builder = new StringBuilder(name.Length);
        var upperNext = false;
        foreach (var character in name)
        {
            builder.Append(upperNext ? char.ToUpperInvariant(character) : character);
            upperNext = character == '_';
        }

        return builder.Replace("_", string.Empty).ToString();
    }
}
